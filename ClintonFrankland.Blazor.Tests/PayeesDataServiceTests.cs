using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class PayeesDataServiceTests
{
    [Fact]
    public async Task GetPayeeSummariesAsync_AggregatesWindowsAndExcludesDeletedByDefault()
    {
        await using var db = CreateDbContext();
        SeedRequiredLookups(db);
        db.Payees.AddRange(
            new Payee { PayeeId = 1, PayeeName = "Power Co", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 2, PayeeName = "Archived", UserId = 42, IsDeleted = true },
            new Payee { PayeeId = 3, PayeeName = "Other User", UserId = 99, IsDeleted = false });
        db.Budgets.Add(new Budget { BudgetId = 1, BudgetName = "Electric", BudgetTypeId = 1, CategoryId = 1, UserId = 42, PayeeId = 1 });
        db.Transactions.AddRange(
            Transaction(1, 42, 1, new DateOnly(2026, 6, 20), -25m),
            Transaction(2, 42, 1, new DateOnly(2026, 5, 10), 100m),
            Transaction(3, 42, 1, new DateOnly(2025, 12, 24), -50m),
            Transaction(4, 42, 1, new DateOnly(2025, 5, 1), -500m),
            Transaction(5, 99, 3, new DateOnly(2026, 6, 20), -999m));
        await db.SaveChangesAsync();

        var result = await new PayeesDataService(db).GetPayeeSummariesAsync(42, includeDeleted: false, new DateOnly(2026, 6, 24));

        var payee = Assert.Single(result);
        Assert.Equal("Power Co", payee.PayeeName);
        Assert.Equal(new DateOnly(2026, 6, 20), payee.LastTransactionDate);
        Assert.Equal(1, payee.BudgetCount);
        Assert.Equal(25m, payee.Expense30Days);
        Assert.Equal(0m, payee.Income30Days);
        Assert.Equal(100m, payee.Income6Months);
        Assert.Equal(75m, payee.Expense6Months);
        Assert.Equal(100m, payee.Income1Year);
        Assert.Equal(75m, payee.Expense1Year);
    }

    [Fact]
    public async Task GetPayeeSummariesAsync_CanIncludeDeletedPayees()
    {
        await using var db = CreateDbContext();
        db.Payees.AddRange(
            new Payee { PayeeId = 1, PayeeName = "Active", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 2, PayeeName = "Archived", UserId = 42, IsDeleted = true });
        await db.SaveChangesAsync();

        var result = await new PayeesDataService(db).GetPayeeSummariesAsync(42, includeDeleted: true, new DateOnly(2026, 6, 24));

        Assert.Equal(["Active", "Archived"], result.Select(p => p.PayeeName).OrderBy(n => n));
    }

    [Fact]
    public async Task UpdatePayeeAsync_ValidatesNameAndUserScope()
    {
        await using var db = CreateDbContext();
        db.Payees.AddRange(
            new Payee { PayeeId = 1, PayeeName = "Power Co", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 2, PayeeName = "Water Co", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 3, PayeeName = "Other User Payee", UserId = 99, IsDeleted = false });
        await db.SaveChangesAsync();

        var service = new PayeesDataService(db);
        await service.UpdatePayeeAsync(42, 1, "  Power Company  ");

        Assert.Equal("Power Company", (await db.Payees.FindAsync(1))!.PayeeName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePayeeAsync(42, 2, "Power Company"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePayeeAsync(42, 3, "Nope"));
    }

    [Fact]
    public async Task MergePayeesAsync_ReassignsTransactionsAndBudgetsThenSoftDeletesSource()
    {
        await using var db = CreateDbContext();
        SeedRequiredLookups(db);
        db.Payees.AddRange(
            new Payee { PayeeId = 1, PayeeName = "Keep", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 2, PayeeName = "Remove", UserId = 42, IsDeleted = false });
        db.Budgets.Add(new Budget { BudgetId = 1, BudgetName = "Bill", BudgetTypeId = 1, CategoryId = 1, UserId = 42, PayeeId = 2 });
        db.Transactions.Add(Transaction(1, 42, 2, new DateOnly(2026, 6, 20), -25m));
        await db.SaveChangesAsync();

        var service = new PayeesDataService(db);
        var preview = await service.PreviewMergeAsync(42, 1, 2);
        var result = await service.MergePayeesAsync(42, 1, 2, "Keep");

        Assert.Equal(1, preview.TransactionCount);
        Assert.Equal(1, preview.BudgetCount);
        Assert.Equal(1, result.TransactionsUpdated);
        Assert.Equal(1, result.BudgetsUpdated);
        Assert.True((await db.Payees.FindAsync(2))!.IsDeleted);
        Assert.Equal(1, (await db.Transactions.FindAsync(1))!.PayeeId);
        Assert.Equal(1, (await db.Budgets.FindAsync(1))!.PayeeId);
    }

    [Fact]
    public async Task MergePayeesAsync_RejectsInvalidOrUnsafeMerges()
    {
        await using var db = CreateDbContext();
        db.Payees.AddRange(
            new Payee { PayeeId = 1, PayeeName = "Keep", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 2, PayeeName = "Remove", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 3, PayeeName = "Deleted", UserId = 42, IsDeleted = true },
            new Payee { PayeeId = 4, PayeeName = "Other User", UserId = 99, IsDeleted = false });
        await db.SaveChangesAsync();

        var service = new PayeesDataService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewMergeAsync(42, 1, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewMergeAsync(42, 1, 3));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewMergeAsync(42, 1, 4));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MergePayeesAsync(42, 1, 2, "Wrong"));
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static void SeedRequiredLookups(ClintonFranklandDbContext db)
    {
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Utilities", UserId = 42 });
        db.Accounts.Add(new Account
        {
            AccountId = 1,
            AccountName = "Checking",
            AccountTypeId = 1,
            BeginningBalance = 0,
            UserId = 42
        });
    }

    private static Transaction Transaction(int id, int userId, int payeeId, DateOnly date, decimal amount) => new()
    {
        TransactionId = id,
        UserId = userId,
        PayeeId = payeeId,
        CategoryId = 1,
        AccountId = 1,
        TransactionDate = date,
        Amount = amount,
        Cleared = false
    };
}
