using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClintonFrankland.Blazor.Tests;

public class BillDueNotificationWorkerTests
{
    [Fact]
    public async Task RunOnceAsync_SendsOnlyAccessibleSharedBudgetBillsAndNamesBudgetsWhenUserHasMultipleMemberships()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 6, 13, 0, 0, TimeSpan.Zero);
        var sent = new List<SentEmail>();
        await using var provider = CreateProvider(nowUtc, sent, db =>
        {
            SeedSettings(db);
            db.Users.AddRange(
                User(1, "owner", "Owner", "owner@example.com"),
                User(2, "viewer", "Viewer", "viewer@example.com"),
                User(3, "outsider", "Outsider", "outsider@example.com"),
                User(4, "removed", "Removed", "removed@example.com"));
            db.SharedBudgets.AddRange(
                SharedBudget(1, "Household", 1),
                SharedBudget(2, "Vacation", 2),
                SharedBudget(3, "Private", 3));
            db.BudgetMembers.AddRange(
                Member(1, 1, 1, BudgetMemberRole.Owner),
                Member(2, 1, 2, BudgetMemberRole.Viewer),
                Member(3, 2, 2, BudgetMemberRole.Viewer),
                Member(4, 3, 3, BudgetMemberRole.Owner),
                Member(5, 1, 4, BudgetMemberRole.Viewer, BudgetMemberStatus.Removed));
            db.Payees.AddRange(
                new Payee { PayeeId = 1, PayeeName = "Power", UserId = 1, IsDeleted = false },
                new Payee { PayeeId = 2, PayeeName = "Hotel", UserId = 2, IsDeleted = false },
                new Payee { PayeeId = 3, PayeeName = "Private", UserId = 3, IsDeleted = false });
            db.Categories.AddRange(
                new Category { CategoryId = 1, CategoryName = "Utilities", UserId = 1, SharedBudgetId = 1 },
                new Category { CategoryId = 2, CategoryName = "Travel", UserId = 2, SharedBudgetId = 2 },
                new Category { CategoryId = 3, CategoryName = "Hidden", UserId = 3, SharedBudgetId = 3 });
            db.Budgets.AddRange(
                Bill(1, 1, 1, "Electric", 1, nowUtc.DateTime.AddDays(2), 80m, sharedBudgetId: 1),
                Bill(2, 2, 2, "Hotel", 2, nowUtc.DateTime.AddDays(4), 120m, sharedBudgetId: 2),
                Bill(3, 3, 3, "Private Bill", 3, nowUtc.DateTime.AddDays(1), 999m, sharedBudgetId: 3));
        });

        await AssertSeedVisibleAsync(provider);
        await RunWorkerAsync(provider);

        Assert.DoesNotContain(sent, email => email.To == "removed@example.com");

        var viewerEmails = sent.Where(email => email.To == "viewer@example.com").ToList();
        Assert.Equal(2, viewerEmails.Count);

        var daily = Assert.Single(viewerEmails, email => email.Subject.StartsWith("Bill due soon:", StringComparison.Ordinal));
        Assert.Contains("Household: Electric", daily.Subject);
        Assert.Contains("Budget: Household", daily.Body);
        Assert.DoesNotContain("Private Bill", daily.Body);

        var weekly = Assert.Single(viewerEmails, email => email.Subject.StartsWith("Upcoming bills this week:", StringComparison.Ordinal));
        Assert.Contains("Household: Electric", weekly.Body);
        Assert.Contains("Vacation: Hotel", weekly.Body);
        Assert.DoesNotContain("Private Bill", weekly.Body);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var viewerLogs = await db.NotificationSendLogs
            .Where(log => log.UserId == 2)
            .OrderBy(log => log.NoticeType)
            .ToListAsync();
        Assert.Equal(2, viewerLogs.Count);
        Assert.Contains(viewerLogs, log => log.NoticeType == "DueSoon" && log.SharedBudgetId == 1);
        Assert.Contains(viewerLogs, log => log.NoticeType == "WeeklyUpcoming" && log.BudgetId == null);
    }

    [Fact]
    public async Task RunOnceAsync_PreservesSingleUserBillNoticeSubjectAndScopesDashboardAndReports()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 6, 13, 0, 0, TimeSpan.Zero);
        var sent = new List<SentEmail>();
        await using var provider = CreateProvider(nowUtc, sent, db =>
        {
            SeedSettings(db);
            db.Users.AddRange(
                User(42, "clinton", "Clinton", "clinton@example.com"),
                User(99, "other", "Other", "other@example.com"));
            db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
            db.Accounts.AddRange(
                new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 100m, UserId = 42, IsDefault = true },
                new Account { AccountId = 2, AccountName = "Other", AccountTypeId = 1, BeginningBalance = 900m, UserId = 99, IsDefault = true });
            db.Categories.AddRange(
                new Category { CategoryId = 1, CategoryName = "Utilities", UserId = 42 },
                new Category { CategoryId = 2, CategoryName = "Other Utilities", UserId = 99 });
            db.Payees.AddRange(
                new Payee { PayeeId = 1, PayeeName = "Power", UserId = 42, IsDeleted = false },
                new Payee { PayeeId = 2, PayeeName = "Other Power", UserId = 99, IsDeleted = false });
            db.Frequencies.Add(new Frequency { FrequencyId = 4, FrequencyName = "Monthly", Sort = 1 });
            db.Transactions.AddRange(
                new Transaction
                {
                    TransactionId = 1,
                    UserId = 42,
                    AccountId = 1,
                    CategoryId = 1,
                    PayeeId = 1,
                    TransactionDate = new DateOnly(2026, 7, 2),
                    Amount = -25m
                },
                new Transaction
                {
                    TransactionId = 2,
                    UserId = 99,
                    AccountId = 2,
                    CategoryId = 2,
                    PayeeId = 2,
                    TransactionDate = new DateOnly(2026, 7, 2),
                    Amount = -900m
                });
            db.Budgets.AddRange(
                Bill(1, 42, 1, "Electric", 1, nowUtc.DateTime.AddDays(2), 30m),
                Bill(2, 99, 2, "Other Electric", 2, nowUtc.DateTime.AddDays(2), 999m));
        });

        await AssertSeedVisibleAsync(provider);
        await RunWorkerAsync(provider);

        var daily = Assert.Single(sent, email =>
            email.To == "clinton@example.com" &&
            email.Subject.StartsWith("Bill due soon:", StringComparison.Ordinal));
        Assert.Equal("Bill due soon: Electric (2026-07-08)", daily.Subject);
        Assert.DoesNotContain("Budget:", daily.Body);
        Assert.DoesNotContain("Other Electric", daily.Body);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var dashboard = await new DashboardDataService(
            new CheckbookDataService(db),
            new BudgetScheduleService(db))
            .GetSnapshotAsync(42, new DateTime(2026, 7, 6));
        var insights = await new InsightsDataService(db).GetMonthlyCategoryTotalsAsync(42, new DateOnly(2026, 7, 1));

        Assert.Equal(75m, dashboard.TodayBalance);
        Assert.Equal(30m, dashboard.UpcomingBillsTotal);
        Assert.DoesNotContain(dashboard.UpcomingBills, bill => bill.Name == "Other Electric");
        var category = Assert.Single(insights);
        Assert.Equal("Utilities", category.CategoryName);
        Assert.Equal(25m, category.Total);
    }

    private static async Task RunWorkerAsync(ServiceProvider provider)
    {
        var worker = new BillDueNotificationWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BillDueNotificationWorker>.Instance,
            provider.GetRequiredService<TimeProvider>());
        await worker.RunOnceAsync(CancellationToken.None);
    }

    private static async Task AssertSeedVisibleAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var userCount = await db.Users.CountAsync();
        var billCount = await db.Budgets.CountAsync();
        var settingsEnabled = await db.BillDueNotificationSettings.AnyAsync(s => s.Id == 1 && s.IsEnabled);
        var smtpEnabled = await db.SmtpSettings.AnyAsync(s => s.Id == 1 && s.IsEnabled);

        Assert.True(
            userCount > 0 && billCount > 0 && settingsEnabled && smtpEnabled,
            $"Seed not visible: users={userCount}, bills={billCount}, billSettings={settingsEnabled}, smtp={smtpEnabled}");
    }

    private static ServiceProvider CreateProvider(
        DateTimeOffset nowUtc,
        List<SentEmail> sent,
        Action<ClintonFranklandDbContext> seed)
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var dbOptions = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), databaseRoot)
            .Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new ClintonFranklandDbContext(dbOptions));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(nowUtc));
        services.AddSingleton(sent);
        services.AddScoped<IEmailSender, CapturingEmailSender>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        seed(db);
        db.SaveChanges();
        return provider;
    }

    private static void SeedSettings(ClintonFranklandDbContext db)
    {
        db.BillDueNotificationSettings.Add(new BillDueNotificationSetting
        {
            Id = 1,
            IsEnabled = true,
            DueSoonDays = 3,
            PastDueEnabled = true,
            PastDueMaxDays = 30,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.SmtpSettings.Add(new SmtpSetting
        {
            Id = 1,
            IsEnabled = true,
            SenderEmail = "budget@example.com",
            TlsMode = SmtpTlsMode.None,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    private static SharedBudget SharedBudget(int id, string name, int ownerUserId) => new()
    {
        SharedBudgetId = id,
        Name = name,
        OwnerUserId = ownerUserId,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static BudgetMember Member(
        int id,
        int sharedBudgetId,
        int userId,
        BudgetMemberRole role,
        BudgetMemberStatus status = BudgetMemberStatus.Active) => new()
    {
        BudgetMemberId = id,
        SharedBudgetId = sharedBudgetId,
        UserId = userId,
        Role = role,
        Status = status,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static Budget Bill(
        int id,
        int userId,
        int categoryId,
        string name,
        int payeeId,
        DateTime dueDate,
        decimal amount,
        int? sharedBudgetId = null) => new()
    {
        BudgetId = id,
        UserId = userId,
        SharedBudgetId = sharedBudgetId,
        BudgetName = name,
        BudgetTypeId = 1,
        CategoryId = categoryId,
        PayeeId = payeeId,
        FrequencyId = 4,
        NextDueDate = dueDate,
        EndDate = new DateTime(1970, 1, 1),
        Amount = amount,
        IsBill = true
    };

    private static User User(int id, string userName, string displayName, string? email) => new()
    {
        UserId = id,
        SiteId = 1,
        UserName = userName,
        DisplayName = displayName,
        EmailAddress = email,
        ReceiveBillDueNotices = true,
        NotificationTimezone = "UTC",
        NotificationDeliveryTime = TimeOnly.MinValue,
        IsAdmin = false,
        Salt = "salt",
        PasswordHash = "hash",
        IsDeleted = false,
        FirstLogin = DateTime.UtcNow,
        LastLogin = DateTime.UtcNow
    };

    private sealed record SentEmail(string To, string Subject, string Body);

    private sealed class CapturingEmailSender : IEmailSender
    {
        private readonly List<SentEmail> _sent;

        public CapturingEmailSender(List<SentEmail> sent)
        {
            _sent = sent;
        }

        public Task SendAsync(string toEmail, string subject, string body)
        {
            _sent.Add(new SentEmail(toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _nowUtc;

        public FixedTimeProvider(DateTimeOffset nowUtc)
        {
            _nowUtc = nowUtc;
        }

        public override DateTimeOffset GetUtcNow() => _nowUtc;
    }
}
