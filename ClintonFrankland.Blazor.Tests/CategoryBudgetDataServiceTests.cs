using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Blazor.Tests;

public class CategoryBudgetDataServiceTests
{
    [Fact]
    public async Task GetMonthRowsAsync_IsUserScopedExpenseOnlyAndUsesMonthBoundaries()
    {
        await using var db = CreateDbContext();
        SeedBaseData(db);
        db.CategoryBudgetTargets.Add(new CategoryBudgetTarget
        {
            CategoryBudgetTargetId = 1,
            UserId = 42,
            CategoryId = 1,
            BudgetMonth = new DateOnly(2026, 6, 1),
            PlannedAmount = 100m
        });
        db.Transactions.AddRange(
            Transaction(1, 42, 1, new DateOnly(2026, 6, 1), -25m),
            Transaction(2, 42, 1, new DateOnly(2026, 6, 30), -35m),
            Transaction(3, 42, 1, new DateOnly(2026, 7, 1), -999m),
            Transaction(4, 42, 1, new DateOnly(2026, 6, 5), 500m),
            Transaction(5, 99, 3, new DateOnly(2026, 6, 5), -999m),
            Transaction(6, 42, 1, new DateOnly(2026, 6, 6), -999m, accountId: 2));
        await db.SaveChangesAsync();

        var rows = await CreateService(db).GetMonthRowsAsync(42, new DateOnly(2026, 6, 15));

        var groceries = Assert.Single(rows);
        Assert.Equal("Groceries", groceries.CategoryName);
        Assert.Equal(100m, groceries.PlannedAmount);
        Assert.Equal(60m, groceries.ActualAmount);
        Assert.Equal(40m, groceries.RemainingAmount);
        Assert.Equal(60m, groceries.PercentUsed);
        Assert.Equal(CategoryBudgetAlertStatus.None, groceries.AlertStatus);
    }

    [Fact]
    public async Task GetMonthRowsAsync_ReturnsWarningAndOverspentStates()
    {
        await using var db = CreateDbContext();
        SeedBaseData(db);
        db.CategoryBudgetTargets.AddRange(
            Target(1, 42, 1, 100m),
            Target(2, 42, 2, 50m));
        db.Transactions.AddRange(
            Transaction(1, 42, 1, new DateOnly(2026, 6, 3), -80m),
            Transaction(2, 42, 2, new DateOnly(2026, 6, 3), -55m));
        await db.SaveChangesAsync();

        var rows = await CreateService(db, warningPercent: 80).GetMonthRowsAsync(42, new DateOnly(2026, 6, 1));

        var groceries = rows.Single(x => x.CategoryName == "Groceries");
        Assert.Equal(CategoryBudgetAlertStatus.Warning, groceries.AlertStatus);
        Assert.Equal(80m, groceries.PercentUsed);

        var utilities = rows.Single(x => x.CategoryName == "Utilities");
        Assert.Equal(CategoryBudgetAlertStatus.Overspent, utilities.AlertStatus);
        Assert.Equal(-5m, utilities.RemainingAmount);
        Assert.Equal(110m, utilities.PercentUsed);
    }

    [Fact]
    public async Task GetMonthRowsAsync_IncludesSpendingWithoutBudgetTargetGracefully()
    {
        await using var db = CreateDbContext();
        SeedBaseData(db);
        db.Transactions.Add(Transaction(1, 42, 2, new DateOnly(2026, 6, 3), -25m));
        await db.SaveChangesAsync();

        var rows = await CreateService(db).GetMonthRowsAsync(42, new DateOnly(2026, 6, 1));

        var row = Assert.Single(rows);
        Assert.Null(row.TargetId);
        Assert.Equal("Utilities", row.CategoryName);
        Assert.Equal(0m, row.PlannedAmount);
        Assert.Equal(25m, row.ActualAmount);
        Assert.Equal(CategoryBudgetAlertStatus.None, row.AlertStatus);
    }

    [Fact]
    public async Task SaveTargetAsync_CreatesUpdatesAndDeletesUserScopedTargets()
    {
        await using var db = CreateDbContext();
        SeedBaseData(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SaveTargetAsync(42, new CategoryBudgetSaveRequest(null, 1, new DateOnly(2026, 6, 20), 100.125m));
        var created = Assert.Single(db.CategoryBudgetTargets);
        Assert.Equal(new DateOnly(2026, 6, 1), created.BudgetMonth);
        Assert.Equal(100.13m, created.PlannedAmount);

        await service.SaveTargetAsync(42, new CategoryBudgetSaveRequest(created.CategoryBudgetTargetId, 2, new DateOnly(2026, 6, 1), 75m));
        var updated = Assert.Single(db.CategoryBudgetTargets);
        Assert.Equal(2, updated.CategoryId);
        Assert.Equal(75m, updated.PlannedAmount);

        await service.DeleteTargetAsync(99, updated.CategoryBudgetTargetId);
        Assert.Single(db.CategoryBudgetTargets);

        await service.DeleteTargetAsync(42, updated.CategoryBudgetTargetId);
        Assert.Empty(db.CategoryBudgetTargets);
    }

    [Fact]
    public async Task SaveTargetAsync_RejectsInvalidAmountCategoryAndDuplicateMove()
    {
        await using var db = CreateDbContext();
        SeedBaseData(db);
        db.CategoryBudgetTargets.AddRange(
            Target(1, 42, 1, 100m),
            Target(2, 42, 2, 50m));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveTargetAsync(42, new CategoryBudgetSaveRequest(null, 1, new DateOnly(2026, 6, 1), -1m)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveTargetAsync(42, new CategoryBudgetSaveRequest(null, 3, new DateOnly(2026, 6, 1), 1m)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveTargetAsync(42, new CategoryBudgetSaveRequest(1, 2, new DateOnly(2026, 6, 1), 1m)));
    }

    [Fact]
    public void Constructor_RejectsInvalidWarningThresholds()
    {
        using var db = CreateDbContext();

        Assert.Throws<InvalidOperationException>(() => CreateService(db, warningPercent: 0));
        Assert.Throws<InvalidOperationException>(() => CreateService(db, warningPercent: 101));
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static CategoryBudgetDataService CreateService(ClintonFranklandDbContext db, int warningPercent = 80) =>
        new(db, Options.Create(new CategoryBudgetAlertOptions { WarningPercent = warningPercent }));

    private static void SeedBaseData(ClintonFranklandDbContext db)
    {
        db.Accounts.AddRange(
            new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 0, UserId = 42 },
            new Account { AccountId = 2, AccountName = "Other Checking", AccountTypeId = 1, BeginningBalance = 0, UserId = 99 });
        db.Categories.AddRange(
            new Category { CategoryId = 1, CategoryName = "Groceries", UserId = 42 },
            new Category { CategoryId = 2, CategoryName = "Utilities", UserId = 42 },
            new Category { CategoryId = 3, CategoryName = "Other User Category", UserId = 99 });
    }

    private static CategoryBudgetTarget Target(int id, int userId, int categoryId, decimal amount) => new()
    {
        CategoryBudgetTargetId = id,
        UserId = userId,
        CategoryId = categoryId,
        BudgetMonth = new DateOnly(2026, 6, 1),
        PlannedAmount = amount
    };

    private static Transaction Transaction(int id, int userId, int categoryId, DateOnly date, decimal amount, int? accountId = null) => new()
    {
        TransactionId = id,
        UserId = userId,
        PayeeId = 1,
        CategoryId = categoryId,
        AccountId = accountId ?? (userId == 42 ? 1 : 2),
        TransactionDate = date,
        Amount = amount,
        Cleared = false
    };
}
