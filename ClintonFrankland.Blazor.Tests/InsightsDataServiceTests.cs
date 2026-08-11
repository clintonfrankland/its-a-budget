using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class InsightsDataServiceTests
{
    [Fact]
    public async Task GetMonthlyCategoryTotalsAsync_IsUserScopedExpenseOnlyAndSorted()
    {
        await using var db = CreateDbContext();
        SeedLookups(db);
        db.Transactions.AddRange(
            Transaction(1, 42, 1, 1, new DateOnly(2026, 6, 3), -75m),
            Transaction(2, 42, 1, 2, new DateOnly(2026, 6, 4), -25m),
            Transaction(3, 42, 1, 1, new DateOnly(2026, 6, 5), 500m),
            Transaction(4, 42, 1, 1, new DateOnly(2026, 5, 31), -999m),
            Transaction(5, 99, 3, 3, new DateOnly(2026, 6, 3), -999m));
        await db.SaveChangesAsync();

        var result = await new InsightsDataService(db).GetMonthlyCategoryTotalsAsync(42, new DateOnly(2026, 6, 15));

        Assert.Equal(["Groceries", "Utilities"], result.Select(x => x.CategoryName));
        Assert.Equal([75m, 25m], result.Select(x => x.Total));
    }

    [Fact]
    public async Task GetMonthlyCategoryTotalsAsync_UsesUncategorizedFallbackForMissingOrOutOfScopeCategory()
    {
        await using var db = CreateDbContext();
        SeedLookups(db);
        db.Transactions.AddRange(
            Transaction(1, 42, 1, 4, new DateOnly(2026, 6, 3), -20m),
            Transaction(2, 42, 1, 3, new DateOnly(2026, 6, 4), -30m));
        await db.SaveChangesAsync();

        var result = await new InsightsDataService(db).GetMonthlyCategoryTotalsAsync(42, new DateOnly(2026, 6, 1));

        var item = Assert.Single(result);
        Assert.Equal(InsightsDataService.UncategorizedCategoryName, item.CategoryName);
        Assert.Equal(50m, item.Total);
    }

    [Fact]
    public async Task GetCategoryTrendAsync_ReturnsPreviousSixMonthsPlusSelectedMonthWithZeroes()
    {
        await using var db = CreateDbContext();
        SeedLookups(db);
        db.Transactions.AddRange(
            Transaction(1, 42, 1, 1, new DateOnly(2025, 12, 31), -10m),
            Transaction(2, 42, 1, 1, new DateOnly(2026, 2, 1), -20m),
            Transaction(3, 42, 1, 1, new DateOnly(2026, 6, 29), -30m),
            Transaction(4, 42, 1, 2, new DateOnly(2026, 6, 10), -40m));
        await db.SaveChangesAsync();

        var service = new InsightsDataService(db);
        var months = InsightsDataService.GetTrendMonths(new DateOnly(2026, 6, 15));
        var result = await service.GetCategoryTrendAsync(42, new DateOnly(2026, 6, 15));

        Assert.Equal(["Dec 2025", "Jan 2026", "Feb 2026", "Mar 2026", "Apr 2026", "May 2026", "Jun 2026"], months.Select(m => m.Label));
        var groceries = result.Single(x => x.CategoryName == "Groceries");
        Assert.Equal([10m, 0m, 20m, 0m, 0m, 0m, 30m], groceries.MonthlyTotals);
    }

    [Fact]
    public async Task GetCategoryTrendAsync_DoesNotExposeMismatchedCategoryLabelOnReadableSharedTransaction()
    {
        await using var db = CreateDbContext();
        SeedLookups(db);
        db.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 10, Name = "Readable", OwnerUserId = 99 });
        db.BudgetMembers.AddRange(
            new BudgetMember { BudgetMemberId = 1, SharedBudgetId = 10, UserId = 42, Role = BudgetMemberRole.Viewer, Status = BudgetMemberStatus.Active },
            new BudgetMember { BudgetMemberId = 2, SharedBudgetId = 10, UserId = 99, Role = BudgetMemberRole.Owner, Status = BudgetMemberStatus.Active });
        db.Accounts.Add(new Account { AccountId = 10, AccountName = "Shared", AccountTypeId = 1, UserId = 99, SharedBudgetId = 10 });
        db.Transactions.Add(new Transaction
        {
            TransactionId = 10,
            UserId = 99,
            SharedBudgetId = 10,
            AccountId = 10,
            CategoryId = 3,
            PayeeId = 1,
            TransactionDate = new DateOnly(2026, 6, 10),
            Amount = -45m
        });
        await db.SaveChangesAsync();

        var result = await new InsightsDataService(db).GetCategoryTrendAsync(42, new DateOnly(2026, 6, 1));

        var row = Assert.Single(result);
        Assert.Equal(InsightsDataService.UncategorizedCategoryName, row.CategoryName);
        Assert.Equal(45m, row.MonthlyTotals[^1]);
        Assert.DoesNotContain(result, item => item.CategoryName == "Other User Category");
    }

    [Fact]
    public async Task GetTopPayeesAsync_OrdersBySpendIncludesDeletedHistoricalNamesAndScopesUsers()
    {
        await using var db = CreateDbContext();
        SeedLookups(db);
        db.Transactions.AddRange(
            Transaction(1, 42, 1, 1, new DateOnly(2026, 6, 2), -20m),
            Transaction(2, 42, 1, 1, new DateOnly(2026, 6, 3), -35m),
            Transaction(3, 42, 2, 1, new DateOnly(2026, 6, 4), -90m),
            Transaction(4, 42, 3, 1, new DateOnly(2026, 6, 5), -100m),
            Transaction(5, 99, 3, 3, new DateOnly(2026, 6, 4), -999m),
            Transaction(6, 42, 1, 1, new DateOnly(2026, 6, 6), -999m, accountId: 2));
        await db.SaveChangesAsync();

        var result = await new InsightsDataService(db).GetTopPayeesAsync(42, new DateOnly(2026, 6, 1));

        Assert.Equal([InsightsDataService.UnknownPayeeName, "Archived Store", "Power Co"], result.Select(x => x.PayeeName));
        Assert.Equal([100m, 90m, 55m], result.Select(x => x.Total));
        Assert.Equal(2, result.Single(x => x.PayeeName == "Power Co").TransactionCount);
    }

    [Fact]
    public async Task InsightQueries_ReturnEmptyListsWhenNoDataExists()
    {
        await using var db = CreateDbContext();
        SeedLookups(db);
        var service = new InsightsDataService(db);

        Assert.Empty(await service.GetMonthlyCategoryTotalsAsync(42, new DateOnly(2026, 6, 1)));
        Assert.Empty(await service.GetCategoryTrendAsync(42, new DateOnly(2026, 6, 1)));
        Assert.Empty(await service.GetTopPayeesAsync(42, new DateOnly(2026, 6, 1)));
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static void SeedLookups(ClintonFranklandDbContext db)
    {
        db.Accounts.AddRange(
            new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 0, UserId = 42 },
            new Account { AccountId = 2, AccountName = "Other Checking", AccountTypeId = 1, BeginningBalance = 0, UserId = 99 });
        db.Categories.AddRange(
            new Category { CategoryId = 1, CategoryName = "Groceries", UserId = 42 },
            new Category { CategoryId = 2, CategoryName = "Utilities", UserId = 42 },
            new Category { CategoryId = 3, CategoryName = "Other User Category", UserId = 99 },
            new Category { CategoryId = 4, CategoryName = null, UserId = 42 });
        db.Payees.AddRange(
            new Payee { PayeeId = 1, PayeeName = "Power Co", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 2, PayeeName = "Archived Store", UserId = 42, IsDeleted = true },
            new Payee { PayeeId = 3, PayeeName = "Other User Payee", UserId = 99, IsDeleted = false });
    }

    private static Transaction Transaction(int id, int userId, int payeeId, int categoryId, DateOnly date, decimal amount, int? accountId = null) => new()
    {
        TransactionId = id,
        UserId = userId,
        PayeeId = payeeId,
        CategoryId = categoryId,
        AccountId = accountId ?? (userId == 42 ? 1 : 2),
        TransactionDate = date,
        Amount = amount,
        Cleared = false
    };
}
