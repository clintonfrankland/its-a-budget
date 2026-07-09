using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

/// <summary>
/// Background worker that sends bill-due email reminders.
/// Runs inside the ASP.NET process so it does not depend on an active Blazor UI circuit.
/// </summary>
public class BillDueNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillDueNotificationWorker> _logger;
    private readonly TimeProvider _timeProvider;

    public BillDueNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BillDueNotificationWorker> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BillDueNotificationWorker starting");

        // Tick frequently enough to be reliable, but do real work only once per user per local day.
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "BillDueNotificationWorker tick failed");
                }
            }
        }
        finally
        {
            timer.Dispose();
            _logger.LogInformation("BillDueNotificationWorker stopping");
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var weeklyDigest = scope.ServiceProvider.GetRequiredService<WeeklyUpcomingBillsDigestService>();

        // Server-wide gate.
        var billSettings = await db.BillDueNotificationSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (billSettings is null)
        {
            billSettings = new BillDueNotificationSetting { Id = 1, UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime };
            db.BillDueNotificationSettings.Add(billSettings);
            await db.SaveChangesAsync(ct);
        }

        if (!billSettings.IsEnabled)
            return;

        // SMTP gate (must be enabled and configured).
        var smtp = await db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (smtp is null || !smtp.IsEnabled)
            return;

        // Load candidate users.
        var users = await db.Users.AsNoTracking()
            .Where(u =>
                !u.IsDeleted &&
                (u.ReceiveBillDueNotices || u.ReceiveWeeklyUpcomingBillDigest) &&
                u.EmailAddress != null &&
                u.EmailAddress != "")
            .ToListAsync(ct);

        if (users.Count == 0)
            return;

        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();
            var sharedBudgetMemberships = await db.BudgetMembers
                .AsNoTracking()
                .Include(m => m.SharedBudget)
                .Where(m => m.UserId == user.UserId && m.Status == BudgetMemberStatus.Active)
                .ToListAsync(ct);
            var sharedBudgetIds = sharedBudgetMemberships
                .Select(m => m.SharedBudgetId)
                .ToList();
            var sharedBudgetNames = sharedBudgetMemberships
                .GroupBy(m => m.SharedBudgetId)
                .ToDictionary(
                    g => g.Key,
                    g => string.IsNullOrWhiteSpace(g.First().SharedBudget?.Name)
                        ? $"Budget {g.Key}"
                        : g.First().SharedBudget!.Name.Trim());
            var showBudgetNames = sharedBudgetNames.Count > 1;

            var tz = ResolveTimezone(user.NotificationTimezone);
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            // Only deliver after the user-selected delivery time.
            if (TimeOnly.FromDateTime(nowLocal) < user.NotificationDeliveryTime)
                continue;

            var localDate = nowLocal.Date;

            await weeklyDigest.SendScheduledDigestAsync(user, localDate, nowUtc, ct);

            if (!user.ReceiveBillDueNotices)
                continue;

            // Bills for this user.
            var bills = await db.Budgets.AsNoTracking()
                .Include(b => b.Payee)
                .Include(b => b.SharedBudget)
                .Where(b =>
                    (b.SharedBudgetId.HasValue
                        ? sharedBudgetIds.Contains(b.SharedBudgetId.Value)
                        : b.UserId == user.UserId) &&
                    b.IsBill == true &&
                    b.NextDueDate != null)
                .ToListAsync(ct);

            foreach (var bill in bills)
            {
                ct.ThrowIfCancellationRequested();

                // NextDueDate is treated as a date (local/unspecified) in this app.
                // We compare by date in the user's timezone to determine due windows.
                var dueLocal = bill.NextDueDate!.Value.Date;
                var daysUntil = (dueLocal - localDate).Days;

                var noticeType = GetNoticeType(daysUntil, billSettings);
                if (noticeType is null)
                    continue;

                // At most one reminder per user/bill/type per local day.
                var noticeLocalDateKey = localDate; // stored as DateTime (midnight)

                var already = await db.NotificationSendLogs.AsNoTracking().AnyAsync(x =>
                        x.UserId == user.UserId &&
                        x.BudgetId == bill.BudgetId &&
                        x.NoticeType == noticeType &&
                        x.NoticeLocalDate == noticeLocalDateKey,
                    ct);

                if (already)
                    continue;

                var subject = BuildSubject(noticeType, bill, dueLocal, showBudgetNames, sharedBudgetNames);
                var body = BuildBody(noticeType, user, bill, dueLocal, daysUntil, showBudgetNames, sharedBudgetNames);

                var log = new NotificationSendLog
                {
                    CreatedAtUtc = nowUtc,
                    UserId = user.UserId,
                    BudgetId = bill.BudgetId,
                    SharedBudgetId = bill.SharedBudgetId,
                    NoticeType = noticeType,
                    NoticeLocalDate = noticeLocalDateKey,
                    Recipient = user.EmailAddress!,
                    Subject = subject,
                    Status = "Failed" // default until success
                };

                db.NotificationSendLogs.Add(log);
                await db.SaveChangesAsync(ct);

                try
                {
                    await email.SendAsync(user.EmailAddress!, subject, body);
                    log.Status = "Sent";
                    log.ErrorMessage = null;
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send bill notice (UserId={UserId}, BudgetId={BudgetId}, Type={Type})", user.UserId, bill.BudgetId, noticeType);
                    log.Status = "Failed";
                    log.ErrorMessage = ex.Message;
                    await db.SaveChangesAsync(ct);
                }
            }
        }
    }

    private static string? GetNoticeType(int daysUntilDue, BillDueNotificationSetting s)
    {
        if (daysUntilDue == 0) return "DueToday";

        if (daysUntilDue > 0 && daysUntilDue <= Math.Max(0, s.DueSoonDays))
            return "DueSoon";

        if (daysUntilDue < 0 && s.PastDueEnabled)
        {
            var daysPast = Math.Abs(daysUntilDue);
            if (daysPast <= Math.Max(0, s.PastDueMaxDays))
                return "PastDue";
        }

        return null;
    }

    private static string BuildSubject(
        string type,
        Budget bill,
        DateTime dueLocalDate,
        bool showBudgetNames,
        IReadOnlyDictionary<int, string> sharedBudgetNames)
    {
        var name = GetBillName(bill);
        var budgetPrefix = GetBudgetLabel(bill, showBudgetNames, sharedBudgetNames);
        var subjectName = string.IsNullOrWhiteSpace(budgetPrefix) ? name : $"{budgetPrefix}: {name}";
        return type switch
        {
            "DueToday" => $"Bill due today: {subjectName} ({dueLocalDate:yyyy-MM-dd})",
            "PastDue" => $"Past due bill: {subjectName} (was due {dueLocalDate:yyyy-MM-dd})",
            _ => $"Bill due soon: {subjectName} ({dueLocalDate:yyyy-MM-dd})"
        };
    }

    private static string BuildBody(
        string type,
        User user,
        Budget bill,
        DateTime dueLocalDate,
        int daysUntil,
        bool showBudgetNames,
        IReadOnlyDictionary<int, string> sharedBudgetNames)
    {
        var name = GetBillName(bill);
        var budgetName = GetBudgetLabel(bill, showBudgetNames, sharedBudgetNames);
        var payee = bill.Payee?.PayeeName;
        var amount = bill.Amount.HasValue ? bill.Amount.Value.ToString("C") : "(amount not set)";

        var when = type switch
        {
            "DueToday" => "is due today",
            "PastDue" => $"is past due by {Math.Abs(daysUntil)} day(s)",
            _ => $"is due in {daysUntil} day(s)"
        };

        return $"Hi {user.DisplayName},\n\n" +
               $"This is an automated reminder that your bill {name} {when}.\n" +
               (string.IsNullOrWhiteSpace(budgetName) ? "" : $"Budget: {budgetName}\n") +
               $"Due date: {dueLocalDate:yyyy-MM-dd}\n" +
               (string.IsNullOrWhiteSpace(payee) ? "" : $"Payee: {payee}\n") +
               $"Amount: {amount}\n\n" +
               "You can manage notification preferences in your Profile page.\n";
    }

    private static string GetBillName(Budget bill) =>
        string.IsNullOrWhiteSpace(bill.BudgetName) ? $"Bill #{bill.BudgetId}" : bill.BudgetName.Trim();

    private static string GetBudgetLabel(
        Budget bill,
        bool showBudgetNames,
        IReadOnlyDictionary<int, string> sharedBudgetNames)
    {
        if (!showBudgetNames || !bill.SharedBudgetId.HasValue)
            return string.Empty;

        if (sharedBudgetNames.TryGetValue(bill.SharedBudgetId.Value, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;

        return string.IsNullOrWhiteSpace(bill.SharedBudget?.Name)
            ? $"Budget {bill.SharedBudgetId.Value}"
            : bill.SharedBudget.Name.Trim();
    }

    private static TimeZoneInfo ResolveTimezone(string? tzId)
    {
        if (string.IsNullOrWhiteSpace(tzId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
