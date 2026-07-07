using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed record WeeklyUpcomingBillsDigestResult(
    bool Sent,
    bool Skipped,
    string Message,
    int UpcomingBillCount);

public class WeeklyUpcomingBillsDigestService
{
    private const string NoticeType = "WeeklyUpcoming";
    private const string PreviewNoticeType = "WeeklyUpcomingPreview";

    private readonly ClintonFranklandDbContext _db;
    private readonly IEmailSender _email;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WeeklyUpcomingBillsDigestService> _logger;

    public WeeklyUpcomingBillsDigestService(
        ClintonFranklandDbContext db,
        IEmailSender email,
        TimeProvider timeProvider,
        ILogger<WeeklyUpcomingBillsDigestService> logger)
    {
        _db = db;
        _email = email;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<WeeklyUpcomingBillsDigestResult> SendScheduledDigestAsync(
        User user,
        DateTime localDate,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (!user.ReceiveWeeklyUpcomingBillDigest)
            return new WeeklyUpcomingBillsDigestResult(false, true, "User has not opted in to weekly upcoming bill digests.", 0);

        if (localDate.DayOfWeek != DayOfWeek.Monday)
            return new WeeklyUpcomingBillsDigestResult(false, true, "Weekly upcoming bill digests run on Mondays.", 0);

        return await SendDigestAsync(user, localDate.Date, nowUtc, enforceIdempotency: true, ct);
    }

    public async Task<WeeklyUpcomingBillsDigestResult> SendManualPreviewAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.UserId == userId, ct);

        if (user is null)
            return new WeeklyUpcomingBillsDigestResult(false, true, "Unable to load current user.", 0);

        var timezone = ResolveTimezone(user.NotificationTimezone);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timezone).Date;

        return await SendDigestAsync(user, localDate, nowUtc, enforceIdempotency: false, ct);
    }

    private async Task<WeeklyUpcomingBillsDigestResult> SendDigestAsync(
        User user,
        DateTime localDate,
        DateTime nowUtc,
        bool enforceIdempotency,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.EmailAddress))
            return new WeeklyUpcomingBillsDigestResult(false, true, "Set an email address before sending weekly upcoming bill digests.", 0);

        var weekStart = localDate.Date;
        var weekEnd = weekStart.AddDays(7);
        var sharedBudgetMemberships = await _db.BudgetMembers
            .AsNoTracking()
            .Include(member => member.SharedBudget)
            .Where(member => member.UserId == user.UserId && member.Status == BudgetMemberStatus.Active)
            .ToListAsync(ct);

        var sharedBudgetIds = sharedBudgetMemberships
            .Select(member => member.SharedBudgetId)
            .ToList();
        var sharedBudgetNames = sharedBudgetMemberships
            .GroupBy(member => member.SharedBudgetId)
            .ToDictionary(
                group => group.Key,
                group => string.IsNullOrWhiteSpace(group.First().SharedBudget?.Name)
                    ? $"Budget {group.Key}"
                    : group.First().SharedBudget!.Name.Trim());
        var showBudgetNames = sharedBudgetNames.Count > 1;

        var bills = await _db.Budgets.AsNoTracking()
            .Include(budget => budget.Payee)
            .Include(budget => budget.SharedBudget)
            .Where(budget =>
                (budget.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(budget.SharedBudgetId.Value)
                    : budget.UserId == user.UserId) &&
                budget.IsBill == true &&
                budget.NextDueDate != null)
            .ToListAsync(ct);

        var upcoming = bills
            .Select(bill => new WeeklyUpcomingBill(
                bill,
                bill.NextDueDate!.Value.Date,
                bill.Amount ?? 0m))
            .Where(bill => bill.DueLocalDate >= weekStart && bill.DueLocalDate < weekEnd)
            .OrderBy(bill => bill.DueLocalDate)
            .ThenBy(bill => GetBillName(bill.Bill))
            .ToList();

        if (upcoming.Count == 0)
            return new WeeklyUpcomingBillsDigestResult(false, true, "No upcoming bills are due in the next 7 days.", 0);

        if (enforceIdempotency)
        {
            var already = await _db.NotificationSendLogs.AsNoTracking().AnyAsync(log =>
                    log.UserId == user.UserId &&
                    log.BudgetId == null &&
                    log.NoticeType == NoticeType &&
                    log.NoticeLocalDate == weekStart,
                ct);

            if (already)
                return new WeeklyUpcomingBillsDigestResult(false, true, "Weekly upcoming bill digest was already sent for this week.", upcoming.Count);
        }

        var subject = $"Upcoming bills this week: {upcoming.Sum(bill => bill.Amount):C}";
        var body = $"Hi {user.DisplayName},\n\n" +
                   $"Here are your upcoming bills for {weekStart:yyyy-MM-dd} through {weekEnd.AddDays(-1):yyyy-MM-dd}:\n\n" +
                   string.Join("\n", upcoming.Select(bill => BuildWeeklyLine(bill.Bill, bill.DueLocalDate, showBudgetNames, sharedBudgetNames))) +
                   "\n\nYou can manage notification preferences in your Profile page.\n";

        var log = new NotificationSendLog
        {
            CreatedAtUtc = nowUtc,
            UserId = user.UserId,
            BudgetId = null,
            SharedBudgetId = null,
            NoticeType = enforceIdempotency ? NoticeType : PreviewNoticeType,
            NoticeLocalDate = weekStart,
            Recipient = user.EmailAddress!,
            Subject = subject,
            Status = "Failed"
        };

        _db.NotificationSendLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendAsync(user.EmailAddress!, subject, body);
            log.Status = "Sent";
            log.ErrorMessage = null;
            await _db.SaveChangesAsync(ct);
            return new WeeklyUpcomingBillsDigestResult(true, false, $"Weekly upcoming bill digest sent to {user.EmailAddress}.", upcoming.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send weekly upcoming bills digest (UserId={UserId})", user.UserId);
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);
            return new WeeklyUpcomingBillsDigestResult(false, false, $"Failed to send weekly upcoming bill digest: {ex.Message}", upcoming.Count);
        }
    }

    private static string BuildWeeklyLine(
        Budget bill,
        DateTime dueLocalDate,
        bool showBudgetNames,
        IReadOnlyDictionary<int, string> sharedBudgetNames)
    {
        var budgetPrefix = GetBudgetLabel(bill, showBudgetNames, sharedBudgetNames);
        var name = GetBillName(bill);
        var payee = string.IsNullOrWhiteSpace(bill.Payee?.PayeeName) ? "" : $" - {bill.Payee.PayeeName.Trim()}";
        var amount = bill.Amount.HasValue ? bill.Amount.Value.ToString("C") : "(amount not set)";
        var label = string.IsNullOrWhiteSpace(budgetPrefix) ? name : $"{budgetPrefix}: {name}";
        return $"- {dueLocalDate:yyyy-MM-dd}: {label}{payee} - {amount}";
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

    private static TimeZoneInfo ResolveTimezone(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private sealed record WeeklyUpcomingBill(Budget Bill, DateTime DueLocalDate, decimal Amount);
}
