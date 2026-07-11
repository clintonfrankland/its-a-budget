using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
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
        Assert.Equal((100m, 125m, -25m, "Over"), (food.Planned, food.Actual, food.Variance, food.Status));
        Assert.Equal(20m, rows.Single(r => r.CategoryName == "Uncategorized").Actual);
    }

    [Fact]
    public async Task Cashflow_StartsWithAllAccountsAndPostedTransactions_ThenFutureItemsOnly()
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

        Assert.Equal(700m, report.StartingBalance);
        Assert.Equal([700m, 650m, 950m], report.Points.Select(p => p.Balance));
        Assert.Equal((650m, new DateOnly(2026, 7, 12)), (report.LowestBalance, report.LowestDate));
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

    private static ClintonFranklandDbContext CreateDb() => new(new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static void SeedBase(ClintonFranklandDbContext db)
    {
        db.Accounts.Add(new() { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, UserId = 1, BeginningBalance = 1000m });
        db.Categories.AddRange(new() { CategoryId = 1, CategoryName = "Food", UserId = 1 }, new() { CategoryId = 2, CategoryName = "Secret", UserId = 2 });
        db.Payees.Add(new() { PayeeId = 1, PayeeName = "Store", UserId = 1 });
    }
    private static Transaction Tx(int id, int userId, int categoryId, DateOnly date, decimal amount, int accountId = 1) => new() { TransactionId = id, UserId = userId, AccountId = accountId, CategoryId = categoryId, PayeeId = 1, TransactionDate = date, Amount = amount };
}
