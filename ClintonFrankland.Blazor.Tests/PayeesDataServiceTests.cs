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
    public async Task MergePayeesAsync_ReassignsTransactionsAndBudgetsThenSoftDeletesSelectedSources()
    {
        await using var db = CreateDbContext();
        SeedRequiredLookups(db);
        db.Payees.AddRange(
            new Payee { PayeeId = 1, PayeeName = "Keep", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 2, PayeeName = "Remove A", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 3, PayeeName = "Remove B", UserId = 42, IsDeleted = false },
            new Payee { PayeeId = 4, PayeeName = "Not Selected", UserId = 42, IsDeleted = false });
        db.Budgets.Add(new Budget { BudgetId = 1, BudgetName = "Bill", BudgetTypeId = 1, CategoryId = 1, UserId = 42, PayeeId = 2 });
        db.Budgets.Add(new Budget { BudgetId = 2, BudgetName = "Bill 2", BudgetTypeId = 1, CategoryId = 1, UserId = 42, PayeeId = 3 });
        db.Budgets.Add(new Budget { BudgetId = 3, BudgetName = "Other", BudgetTypeId = 1, CategoryId = 1, UserId = 42, PayeeId = 4 });
        db.Transactions.Add(Transaction(1, 42, 2, new DateOnly(2026, 6, 20), -25m));
        db.Transactions.Add(Transaction(2, 42, 3, new DateOnly(2026, 6, 21), -30m));
        db.Transactions.Add(Transaction(3, 42, 4, new DateOnly(2026, 6, 22), -40m));
        await db.SaveChangesAsync();

        var service = new PayeesDataService(db);
        var selectedPayeeIds = new[] { 1, 2, 3 };
        var preview = await service.PreviewMergeAsync(42, 1, selectedPayeeIds);
        var result = await service.MergePayeesAsync(42, 1, selectedPayeeIds, "Keep");

        Assert.Equal(2, preview.TransactionCount);
        Assert.Equal(2, preview.BudgetCount);
        Assert.Equal(["Remove A", "Remove B"], preview.RemovedPayees.Select(p => p.PayeeName));
        Assert.Equal(2, result.TransactionsUpdated);
        Assert.Equal(2, result.BudgetsUpdated);
        Assert.Equal([2, 3], result.RemovedPayeeIds.OrderBy(id => id));
        Assert.True((await db.Payees.FindAsync(2))!.IsDeleted);
        Assert.True((await db.Payees.FindAsync(3))!.IsDeleted);
        Assert.False((await db.Payees.FindAsync(4))!.IsDeleted);
        Assert.Equal(1, (await db.Transactions.FindAsync(1))!.PayeeId);
        Assert.Equal(1, (await db.Transactions.FindAsync(2))!.PayeeId);
        Assert.Equal(4, (await db.Transactions.FindAsync(3))!.PayeeId);
        Assert.Equal(1, (await db.Budgets.FindAsync(1))!.PayeeId);
        Assert.Equal(1, (await db.Budgets.FindAsync(2))!.PayeeId);
        Assert.Equal(4, (await db.Budgets.FindAsync(3))!.PayeeId);
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

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewMergeAsync(42, 1, [1]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewMergeAsync(42, 1, [2, 3]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewMergeAsync(42, 1, [1, 3]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewMergeAsync(42, 1, [1, 4]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MergePayeesAsync(42, 1, [1, 2], "Wrong"));
    }

    [Fact]
    public void PayeesPageSource_UsesSelectedRowsAndRemovesOldRemoveDropdown()
    {
        var componentSource = ReadRepoFile("Components/Pages/Payees.razor");
        var codeBehindSource = ReadRepoFile("Components/Pages/Payees.razor.cs");

        Assert.Contains("selectedPayeeIds.Count < 2", componentSource);
        Assert.Contains("TogglePayeeSelection", componentSource);
        Assert.Contains("Data=\"@selectedPayees\"", componentSource);
        Assert.DoesNotContain("removePayeeDropDown", componentSource);
        Assert.DoesNotContain("removePayeeId", codeBehindSource);
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

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(path))
                return File.ReadAllText(path);

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }
}
