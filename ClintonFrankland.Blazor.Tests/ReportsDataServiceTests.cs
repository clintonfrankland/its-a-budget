using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class ReportsDataServiceTests
{
    [Fact]
    public async Task SpendVsPlan_UsesTargetActualUnionAndUncategorized()
    {
        await using var db = CreateDb();
        SeedBase(db);
        db.CategoryBudgetTargets.Add(new() { UserId = 1, CategoryId = 1, BudgetMonth = new(2026, 7, 1), PlannedAmount = 100m });
        db.Transactions.AddRange(Tx(1, 1, 1, new(2026, 7, 31), -125m), Tx(2, 1, 2, new(2026, 7, 1), -20m));
        await db.SaveChangesAsync();

        var rows = await new ReportsDataService(db).GetSpendVsPlanAsync(1, new(2026, 7, 15));

        var food = rows.Single(r => r.CategoryName == "Food");
        Assert.Equal((100m, 125m, -25m, "Over budget"), (food.Planned, food.Actual, food.Variance, food.Status));
        Assert.Equal(20m, rows.Single(r => r.CategoryName == "Uncategorized").Actual);
        Assert.Equal("Unbudgeted", rows.Single(r => r.CategoryName == "Uncategorized").Status);
    }

    [Fact]
    public void MonthCloseVariance_RoundsClassifiesAndOrdersHighlightsDeterministically()
    {
        var snapshot = MonthCloseVarianceSnapshot.Create(new DateOnly(2026, 7, 20),
        [
            new("Zeta", 20m, 30m), new("Alpha", 10m, 20m), new("No plan", 0m, 9m),
            new("Tiny", 10.004m, 10m), new("Beta", 30m, 10m), new("Able", 25m, 5m),
            new("Charlie", 19m, 4m), new("Delta", 13m, 3m)
        ]);

        Assert.Equal(new DateOnly(2026, 7, 1), snapshot.MonthStart);
        Assert.Equal((127.00m, 91m, 36m), (snapshot.TotalBudgeted, snapshot.TotalActual, snapshot.TotalVariance));
        Assert.Equal(["Alpha", "Zeta", "No plan"], snapshot.NeedsAttention.Select(row => row.CategoryName));
        Assert.Equal(["Able", "Beta", "Charlie"], snapshot.LargestUnderBudget.Select(row => row.CategoryName));
        Assert.Equal(["Alpha", "Zeta", "No plan", "Able", "Beta", "Charlie", "Delta", "Tiny"], snapshot.Rows.Select(row => row.CategoryName));
        Assert.Equal(["Over budget", "Over budget", "Unbudgeted", "Under budget", "Under budget", "Under budget", "Under budget", "On budget"], snapshot.Rows.Select(row => row.Status));
    }

    [Fact]
    public async Task MonthCloseVariance_UsesHalfOpenExpenseBoundaryAndIncludesBudgetOnlyAndActualOnlyRows()
    {
        await using var db = CreateDb(); SeedBase(db);
        db.Categories.Add(new Category { CategoryId = 3, CategoryName = "Utilities", UserId = 1 });
        db.CategoryBudgetTargets.Add(new CategoryBudgetTarget { UserId = 1, CategoryId = 3, BudgetMonth = new(2026, 7, 1), PlannedAmount = 75m });
        db.Transactions.AddRange(
            Tx(1, 1, 1, new(2026, 6, 30), -100m), Tx(2, 1, 1, new(2026, 7, 1), -10m),
            Tx(3, 1, 1, new(2026, 7, 31), -15m), Tx(4, 1, 1, new(2026, 8, 1), -100m),
            Tx(5, 1, 1, new(2026, 7, 15), 999m));
        await db.SaveChangesAsync();

        var snapshot = await new ReportsDataService(db).GetMonthCloseVarianceAsync(1, new(2026, 7, 20));

        Assert.Equal((0m, 25m, -25m, "Unbudgeted"), SnapshotRow(snapshot, "Food"));
        Assert.Equal((75m, 0m, 75m, "Under budget"), SnapshotRow(snapshot, "Utilities"));
    }

    [Fact]
    public async Task MonthCloseVariance_UsesAllowanceEffectiveAndEndDatesThenHistoricalFallback()
    {
        await using var db = CreateDb(); SeedBase(db);
        db.CategoryBudgetTargets.AddRange(
            new CategoryBudgetTarget { UserId = 1, CategoryId = 1, BudgetMonth = new(2026, 6, 1), PlannedAmount = 90m },
            new CategoryBudgetTarget { UserId = 1, CategoryId = 1, BudgetMonth = new(2026, 7, 1), PlannedAmount = 95m });
        db.Budgets.Add(new Budget
        {
            BudgetId = 70, UserId = 1, CategoryId = 1, BudgetName = "Food allowance", BudgetTypeId = 1,
            IsSpendingAllowance = true, FrequencyId = 4, NextDueDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 7, 31), Amount = 125m
        });
        await db.SaveChangesAsync();
        var service = new ReportsDataService(db);

        Assert.Equal(90m, Assert.Single((await service.GetMonthCloseVarianceAsync(1, new(2026, 6, 1))).Rows).Planned);
        Assert.Equal(125m, Assert.Single((await service.GetMonthCloseVarianceAsync(1, new(2026, 7, 1))).Rows).Planned);
        Assert.Empty((await service.GetMonthCloseVarianceAsync(1, new(2026, 8, 1))).Rows);
    }

    [Fact]
    public async Task SpendVsPlan_UsesAllowanceFromItsEffectiveMonthAndPreservesEarlierTargets()
    {
        await using var db = CreateDb();
        SeedBase(db);
        db.CategoryBudgetTargets.Add(new()
        {
            UserId = 1,
            CategoryId = 1,
            BudgetMonth = new(2026, 6, 1),
            PlannedAmount = 500m
        });
        db.Budgets.Add(new Budget
        {
            BudgetId = 50,
            UserId = 1,
            CategoryId = 1,
            BudgetName = "Food",
            BudgetTypeId = 1,
            IsSpendingAllowance = true,
            FrequencyId = 1,
            NextDueDate = new DateTime(2026, 7, 18),
            EndDate = new DateTime(1970, 1, 1),
            Amount = 150m
        });
        await db.SaveChangesAsync();
        var service = new ReportsDataService(db);

        var june = Assert.Single(await service.GetSpendVsPlanAsync(1, new DateOnly(2026, 6, 15)));
        var july = Assert.Single(await service.GetSpendVsPlanAsync(1, new DateOnly(2026, 7, 15)));

        Assert.Equal(500m, june.Planned);
        Assert.Equal(450m, july.Planned);
    }

    [Fact]
    public async Task Cashflow_AsOfDateIncludesRemainingAllowance()
    {
        await using var db = CreateDb();
        SeedBase(db);
        db.Budgets.Add(new Budget
        {
            BudgetId = 60,
            UserId = 1,
            CategoryId = 1,
            BudgetName = "Food",
            BudgetTypeId = 1,
            IsSpendingAllowance = true,
            FrequencyId = 1,
            NextDueDate = new DateTime(2026, 7, 18),
            EndDate = new DateTime(1970, 1, 1),
            Amount = 150m
        });
        db.Transactions.Add(Tx(1, 1, 1, new DateOnly(2026, 7, 12), -40m));
        await db.SaveChangesAsync();

        var report = await new ReportsDataService(db).GetCashflowAsync(1, 30, new DateOnly(2026, 7, 15));

        Assert.Equal(960m, report.StartingBalance);
        Assert.Contains(report.Points, point => point.Description.Contains("allowance", StringComparison.OrdinalIgnoreCase) && point.Change == -110m);
    }

    [Fact]
    public async Task Cashflow_StartsWithDefaultLedgerAndPostedTransactions_ThenFutureItemsOnly()
    {
        await using var db = CreateDb();
        SeedBase(db);
        db.Accounts.Add(new() { AccountId = 2, AccountName = "Liability", AccountTypeId = 1, UserId = 1, BeginningBalance = -200m });
        db.Transactions.Add(Tx(1, 1, 1, new(2026, 7, 10), -100m));
        db.Budgets.AddRange(
            new() { BudgetId = 1, UserId = 1, CategoryId = 1, BudgetTypeId = 1, Amount = 50m, NextDueDate = new(2026, 7, 12), FrequencyId = 0 },
            new() { BudgetId = 2, UserId = 1, CategoryId = 1, BudgetTypeId = 0, Amount = 300m, NextDueDate = new(2026, 7, 20), FrequencyId = 0 });
        await db.SaveChangesAsync();

        var report = await new ReportsDataService(db).GetCashflowAsync(1, 30, new(2026, 7, 11));

        Assert.Equal(900m, report.StartingBalance);
        Assert.Equal([900m, 850m, 1150m], report.Points.Select(p => p.Balance));
        Assert.Equal((850m, new DateOnly(2026, 7, 12)), (report.LowestBalance, report.LowestDate));
    }

    [Fact]
    public async Task Cashflow_EmptyFutureUsesTodayAsEarliestLow_AndRejectsShortHorizon()
    {
        await using var db = CreateDb(); SeedBase(db); await db.SaveChangesAsync();
        var service = new ReportsDataService(db);
        var report = await service.GetCashflowAsync(1, 30, new(2026, 7, 11));
        Assert.Single(report.Points);
        Assert.Equal(new DateOnly(2026, 7, 11), report.LowestDate);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetCashflowAsync(1, 29, new(2026, 7, 11)));
    }

    [Fact]
    public async Task Cashflow_AppliesDebtSignToStartingBalanceWithoutReversingTransactions()
    {
        await using var db = CreateDb();
        db.Accounts.Add(new Account
        {
            AccountId = 1,
            AccountName = "Loan",
            AccountTypeId = 3,
            UserId = 1,
            BeginningBalance = 300m,
            IsDefault = true
        });
        db.Transactions.Add(Tx(1, 1, 1, new(2026, 7, 10), -50m));
        await db.SaveChangesAsync();

        var report = await new ReportsDataService(db).GetCashflowAsync(1, 30, new(2026, 7, 11));

        Assert.Equal(-250m, report.StartingBalance);
        Assert.Equal(-250m, Assert.Single(report.Points).Balance);
    }

    [Fact]
    public async Task NetWorth_ReconstructsSevenMonthEndsAndExcludesOtherUser()
    {
        await using var db = CreateDb(); SeedBase(db);
        db.Accounts.Add(new() { AccountId = 2, AccountName = "Other", AccountTypeId = 1, UserId = 2, BeginningBalance = 9999m });
        db.Transactions.AddRange(Tx(1, 1, 1, new(2026, 1, 31), -100m), Tx(2, 1, 1, new(2026, 7, 1), 50m), Tx(3, 2, 1, new(2026, 7, 1), 9999m, 2));
        await db.SaveChangesAsync();

        var report = await new ReportsDataService(db).GetNetWorthAsync(1, new(2026, 7, 1), new(2026, 7, 11));

        Assert.Equal(7, report.History.Count);
        Assert.Equal(new DateOnly(2026, 1, 31), report.History[0].Date);
        Assert.Equal(950m, report.CurrentTotal);
        Assert.Equal(900m, report.History[0].Value);
    }

    [Fact]
    public async Task NetWorth_AggregatesAssetAndSignedDebtBalancesWithoutChangingTransactionSigns()
    {
        await using var db = CreateDb();
        SeedBase(db);
        db.Accounts.AddRange(
            new Account { AccountId = 2, AccountName = "Loan", AccountTypeId = 3, UserId = 1, BeginningBalance = 300m },
            new Account { AccountId = 3, AccountName = "Negative Loan", AccountTypeId = 3, UserId = 1, BeginningBalance = -75m },
            new Account { AccountId = 4, AccountName = "Paid Loan", AccountTypeId = 3, UserId = 1, BeginningBalance = 0m });
        db.Transactions.AddRange(
            Tx(1, 1, 1, new(2026, 7, 10), -25m, 1),
            Tx(2, 1, 1, new(2026, 7, 10), -50m, 2));
        await db.SaveChangesAsync();

        var report = await new ReportsDataService(db).GetNetWorthAsync(1, new(2026, 7, 1), new(2026, 7, 11));

        Assert.Equal(650m, report.CurrentTotal);
        Assert.Equal(650m, report.History[^1].Value);

        var loan = await db.Accounts.SingleAsync(account => account.AccountId == 2);
        loan.AccountTypeId = 1;
        await db.SaveChangesAsync();

        var changedTypeReport = await new ReportsDataService(db).GetNetWorthAsync(1, new(2026, 7, 1), new(2026, 7, 11));
        Assert.Equal(1150m, changedTypeReport.CurrentTotal);
    }

    [Fact]
    public async Task AllReports_IncludeReadableSharedBudgetAndExcludeUnreadableAndSecondUserData()
    {
        await using var db = CreateDb();
        SeedBase(db);
        db.SharedBudgets.AddRange(
            new SharedBudget { SharedBudgetId = 10, Name = "Readable", OwnerUserId = 2 },
            new SharedBudget { SharedBudgetId = 20, Name = "Hidden", OwnerUserId = 2 });
        db.BudgetMembers.AddRange(
            new BudgetMember { BudgetMemberId = 1, SharedBudgetId = 10, UserId = 1, Role = BudgetMemberRole.Viewer, Status = BudgetMemberStatus.Active },
            new BudgetMember { BudgetMemberId = 2, SharedBudgetId = 10, UserId = 2, Role = BudgetMemberRole.Owner, Status = BudgetMemberStatus.Active });
        db.Categories.AddRange(
            new Category { CategoryId = 10, CategoryName = "Shared Food", UserId = 2, SharedBudgetId = 10 },
            new Category { CategoryId = 20, CategoryName = "Hidden Food", UserId = 2, SharedBudgetId = 20 });
        db.Accounts.AddRange(
            new Account { AccountId = 10, AccountName = "Shared", AccountTypeId = 1, UserId = 2, SharedBudgetId = 10, BeginningBalance = 200m },
            new Account { AccountId = 20, AccountName = "Hidden", AccountTypeId = 1, UserId = 2, SharedBudgetId = 20, BeginningBalance = 9000m },
            new Account { AccountId = 30, AccountName = "Second user", AccountTypeId = 1, UserId = 2, BeginningBalance = 8000m });
        db.CategoryBudgetTargets.AddRange(
            new CategoryBudgetTarget { UserId = 2, SharedBudgetId = 10, CategoryId = 10, BudgetMonth = new(2026, 7, 1), PlannedAmount = 80m },
            new CategoryBudgetTarget { UserId = 2, SharedBudgetId = 20, CategoryId = 20, BudgetMonth = new(2026, 7, 1), PlannedAmount = 900m });
        db.Transactions.AddRange(
            SharedTx(10, 10, 10, 10, new(2026, 7, 2), -25m),
            SharedTx(20, 20, 20, 20, new(2026, 7, 2), -700m),
            Tx(30, 2, 20, new(2026, 7, 2), -600m, 30));
        db.Budgets.AddRange(
            new Budget { BudgetId = 10, UserId = 2, SharedBudgetId = 10, CategoryId = 10, BudgetTypeId = 1, Amount = 30m, NextDueDate = new(2026, 7, 12), FrequencyId = 0 },
            new Budget { BudgetId = 20, UserId = 2, SharedBudgetId = 20, CategoryId = 20, BudgetTypeId = 1, Amount = 500m, NextDueDate = new(2026, 7, 12), FrequencyId = 0 });
        await db.SaveChangesAsync();
        var service = new ReportsDataService(db);

        var spend = await service.GetSpendVsPlanAsync(1, new(2026, 7, 15));
        var trends = await service.GetCategoryTrendsAsync(1, new(2026, 7, 15));
        var cashflow = await service.GetCashflowAsync(1, 30, new(2026, 7, 11));
        var netWorth = await service.GetNetWorthAsync(1, new(2026, 7, 1), new(2026, 7, 11));

        Assert.Equal((80m, 25m), (spend.Single(x => x.CategoryName == "Shared Food").Planned, spend.Single(x => x.CategoryName == "Shared Food").Actual));
        Assert.DoesNotContain(spend, x => x.CategoryName == "Hidden Food");
        Assert.Equal(25m, trends.Single(x => x.CategoryName == "Shared Food").MonthlyTotals[^1]);
        Assert.DoesNotContain(trends, x => x.CategoryName == "Hidden Food");
        Assert.Equal(975m, cashflow.StartingBalance);
        Assert.Equal(945m, cashflow.LowestBalance);
        Assert.Equal(1175m, netWorth.CurrentTotal);
    }

    [Fact]
    public async Task CategoryTrends_UsesExactSevenMonthBoundaryAndZeroFillsGaps()
    {
        await using var db = CreateDb(); SeedBase(db);
        db.Transactions.AddRange(
            Tx(1, 1, 1, new(2025, 12, 31), -999m),
            Tx(2, 1, 1, new(2026, 1, 1), -10m),
            Tx(3, 1, 1, new(2026, 7, 31), -20m),
            Tx(4, 1, 1, new(2026, 8, 1), -999m));
        await db.SaveChangesAsync();

        var row = Assert.Single(await new ReportsDataService(db).GetCategoryTrendsAsync(1, new(2026, 7, 15)));

        Assert.Equal([10m, 0m, 0m, 0m, 0m, 0m, 20m], row.MonthlyTotals);
    }

    private static ClintonFranklandDbContext CreateDb() => new(new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static void SeedBase(ClintonFranklandDbContext db)
    {
        db.Accounts.Add(new() { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, UserId = 1, BeginningBalance = 1000m, IsDefault = true });
        db.Categories.AddRange(new() { CategoryId = 1, CategoryName = "Food", UserId = 1 }, new() { CategoryId = 2, CategoryName = "Secret", UserId = 2 });
        db.Payees.Add(new() { PayeeId = 1, PayeeName = "Store", UserId = 1 });
    }
    private static Transaction Tx(int id, int userId, int categoryId, DateOnly date, decimal amount, int accountId = 1) => new() { TransactionId = id, UserId = userId, AccountId = accountId, CategoryId = categoryId, PayeeId = 1, TransactionDate = date, Amount = amount };
    private static Transaction SharedTx(int id, int sharedBudgetId, int accountId, int categoryId, DateOnly date, decimal amount) => new()
    {
        TransactionId = id, UserId = 2, SharedBudgetId = sharedBudgetId, AccountId = accountId,
        CategoryId = categoryId, PayeeId = 1, TransactionDate = date, Amount = amount
    };
    private static (decimal Planned, decimal Actual, decimal Variance, string Status) SnapshotRow(MonthCloseVarianceSnapshot snapshot, string categoryName)
    {
        var row = snapshot.Rows.Single(item => item.CategoryName == categoryName);
        return (row.Planned, row.Actual, row.Variance, row.Status);
    }
}
