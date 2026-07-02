using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class ServiceRefactorWorkflowTests
{
    [Fact]
    public async Task BudgetScheduleForecast_IsUserScopedOrderedAndCalculatesRunningBalance()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        db.Accounts.AddRange(
            new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 100m, UserId = 42 },
            new Account { AccountId = 2, AccountName = "Other", AccountTypeId = 1, BeginningBalance = 999m, UserId = 99 });
        db.Categories.AddRange(
            new Category { CategoryId = 1, CategoryName = "Income", UserId = 42 },
            new Category { CategoryId = 2, CategoryName = "Bills", UserId = 42 },
            new Category { CategoryId = 3, CategoryName = "Other Bills", UserId = 99 });
        db.Frequencies.Add(new Frequency { FrequencyId = 0, FrequencyName = "One-time", Sort = 0 });
        db.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            UserId = 42,
            AccountId = 1,
            CategoryId = 2,
            PayeeId = 1,
            TransactionDate = new DateOnly(2026, 7, 1),
            Amount = -20m
        });
        db.Payees.Add(new Payee { PayeeId = 1, PayeeName = "Power", UserId = 42, IsDeleted = false });
        db.Budgets.AddRange(
            Budget(1, 42, "Paycheck", 0, 1, new DateTime(2026, 7, 2), 50m),
            Budget(2, 42, "Electric", 1, 2, new DateTime(2026, 7, 2), 30m),
            Budget(3, 99, "Other User Bill", 1, 3, new DateTime(2026, 7, 2), 999m));
        await db.SaveChangesAsync();

        var result = await new BudgetScheduleService(db).GetForecastAsync(42, new DateTime(2026, 7, 3));

        Assert.Equal(["Paycheck", "Electric"], result.Select(x => x.BudgetName));
        Assert.Equal([50m, -30m], result.Select(x => x.Amount));
        Assert.Equal([130m, 100m], result.Select(x => x.Balance));
    }

    [Fact]
    public async Task MarkBudgetPaidAsync_DeletesOneTimeAndTerminalBudgets()
    {
        await using var db = CreateDbContext();
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Bills", UserId = 42 });
        db.Budgets.AddRange(
            Budget(1, 42, "One-time", 1, 1, new DateTime(2026, 7, 2), 25m, frequencyId: 0),
            Budget(2, 42, "Ends", 1, 1, new DateTime(2026, 7, 2), 25m, frequencyId: 1, endDate: new DateTime(2026, 7, 5)),
            Budget(3, 42, "Continues", 1, 1, new DateTime(2026, 7, 2), 25m, frequencyId: 1, endDate: new DateTime(2026, 7, 30)),
            Budget(4, 99, "Other", 1, 1, new DateTime(2026, 7, 2), 25m, frequencyId: 0));
        await db.SaveChangesAsync();

        var service = new BudgetScheduleService(db);
        await service.MarkBudgetPaidAsync(42, 1);
        await service.MarkBudgetPaidAsync(42, 2);
        await service.MarkBudgetPaidAsync(42, 3);

        Assert.Null(await db.Budgets.FindAsync(1));
        Assert.Null(await db.Budgets.FindAsync(2));
        Assert.Equal(new DateTime(2026, 7, 9), (await db.Budgets.FindAsync(3))!.NextDueDate);
        Assert.NotNull(await db.Budgets.FindAsync(4));
    }

    [Fact]
    public async Task AccountsDataService_SaveAndDeleteAreRoundedAndUserScoped()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        var service = new AccountsDataService(db);

        await service.SaveAccountAsync(42, -1, "Checking", "123", 1, 100.129m, 200.129m, 50.129m, 5, 25.129m, 3.456m, "https://bank", DateTime.UtcNow);

        var created = Assert.Single(await db.Accounts.Where(a => a.UserId == 42).ToListAsync());
        Assert.Equal(100.13m, created.Balance);
        Assert.Equal(200.13m, created.CreditLimit);
        Assert.Equal(3.46m, created.InterestRate);

        await service.SaveAccountAsync(99, created.AccountId, "Nope", "", 1, 1m, 0m, 0m, 1, 0m, 0m, "", DateTime.UtcNow);
        Assert.Equal("Checking", (await db.Accounts.FindAsync(created.AccountId))!.AccountName);

        await service.DeleteAccountAsync(99, created.AccountId);
        Assert.NotNull(await db.Accounts.FindAsync(created.AccountId));

        await service.DeleteAccountAsync(42, created.AccountId);
        Assert.Null(await db.Accounts.FindAsync(created.AccountId));
    }

    [Fact]
    public async Task CheckbookDataService_SaveEditDeleteTransactionPathsAreUserScoped()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        db.Accounts.AddRange(
            new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 0m, UserId = 42 },
            new Account { AccountId = 2, AccountName = "Other", AccountTypeId = 1, BeginningBalance = 0m, UserId = 99 });
        await db.SaveChangesAsync();

        var service = new CheckbookDataService(db);
        await service.SaveTransactionAsync(42, -1, new DateOnly(2026, 7, 2), "Power", "Utilities", -12.345m, false, " note ", "/receipts/a.pdf");

        var created = Assert.Single(await db.Transactions.Where(t => t.UserId == 42).ToListAsync());
        Assert.Equal(-12.35m, created.Amount);
        Assert.Equal("note", created.Notes);
        Assert.Equal(1, created.AccountId);
        Assert.Equal("Power", (await db.Payees.FindAsync(created.PayeeId))!.PayeeName);
        Assert.Equal("Utilities", (await db.Categories.FindAsync(created.CategoryId))!.CategoryName);

        await service.SaveTransactionAsync(99, created.TransactionId, new DateOnly(2026, 7, 3), "Nope", "Nope", -1m, true, null, null);
        Assert.Equal(-12.35m, (await db.Transactions.FindAsync(created.TransactionId))!.Amount);

        await service.SaveTransactionAsync(42, created.TransactionId, new DateOnly(2026, 7, 3), "Power", "Utilities", 20m, true, null, null);
        var edited = (await db.Transactions.FindAsync(created.TransactionId))!;
        Assert.Equal(20m, edited.Amount);
        Assert.True(edited.Cleared);

        await service.DeleteTransactionAsync(99, created.TransactionId);
        Assert.NotNull(await db.Transactions.FindAsync(created.TransactionId));

        await service.DeleteTransactionAsync(42, created.TransactionId);
        Assert.Null(await db.Transactions.FindAsync(created.TransactionId));
    }

    [Fact]
    public void TargetPages_DoNotUseDbContextOrEfQueryApis()
    {
        var targetPages = new[]
        {
            "Components/Pages/Budget.razor.cs",
            "Components/Pages/Checkbook.razor.cs",
            "Components/Pages/Accounts.razor.cs",
            "Components/Pages/BudgetItems.razor.cs"
        };

        foreach (var page in targetPages)
        {
            var source = ReadRepoFile(page);
            Assert.DoesNotContain("ClintonFranklandDbContext", source);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source);
            Assert.DoesNotContain(".Include(", source);
            Assert.DoesNotContain(".SaveChanges", source);
            Assert.DoesNotContain(".ToListAsync", source);
            Assert.DoesNotContain(".FirstOrDefaultAsync", source);
        }
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static void SeedAccountTypes(ClintonFranklandDbContext db) =>
        db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });

    private static Budget Budget(
        int id,
        int userId,
        string name,
        int budgetTypeId,
        int categoryId,
        DateTime dueDate,
        decimal amount,
        int frequencyId = 0,
        DateTime? endDate = null) => new()
        {
            BudgetId = id,
            UserId = userId,
            BudgetName = name,
            BudgetTypeId = budgetTypeId,
            CategoryId = categoryId,
            FrequencyId = frequencyId,
            NextDueDate = dueDate,
            EndDate = endDate ?? new DateTime(1970, 1, 1),
            Amount = amount
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
