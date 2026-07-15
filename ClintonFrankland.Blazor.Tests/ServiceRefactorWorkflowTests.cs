using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task BudgetDataService_SaveBudget_RoundsAndValidatesAmountsAndDates()
    {
        await using var db = CreateDbContext();
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Bills", UserId = 42 });
        db.Frequencies.Add(new Frequency { FrequencyId = 4, FrequencyName = "Monthly", Sort = 1 });
        await db.SaveChangesAsync();
        var service = new BudgetDataService(db);

        await service.SaveBudgetAsync(
            42,
            -1,
            "Electric",
            1,
            4,
            new DateTime(2026, 7, 3, 15, 30, 0),
            new DateTime(1970, 1, 1),
            123.455m,
            "Bills",
            "",
            false,
            false,
            false);

        var saved = Assert.Single(await db.Budgets.Where(b => b.UserId == 42).ToListAsync());
        Assert.Equal(123.46m, saved.Amount);
        Assert.Equal(new DateTime(2026, 7, 3, 15, 30, 0), saved.NextDueDate);

        var dateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveBudgetAsync(42, -1, "Bad", 1, 4, new DateTime(2026, 7, 3), new DateTime(2026, 7, 2), 1m, "Bills", "", false, false, false));
        Assert.Equal("End date cannot be before the next due date.", dateException.Message);

        var amountException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveBudgetAsync(42, -1, "Bad", 1, 4, new DateTime(2026, 7, 3), new DateTime(1970, 1, 1), -1m, "Bills", "", false, false, false));
        Assert.Equal(CurrencyPolicy.NonNegativeAmountMessage, amountException.Message);
    }

    [Fact]
    public async Task BudgetItemsDataService_SaveBudget_RoundsAndValidatesAmountsAndDates()
    {
        await using var db = CreateDbContext();
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Bills", UserId = 42 });
        db.Frequencies.Add(new Frequency { FrequencyId = 4, FrequencyName = "Monthly", Sort = 1 });
        await db.SaveChangesAsync();
        var service = new BudgetItemsDataService(db);

        await service.SaveBudgetAsync(
            42,
            -1,
            "Rent",
            1,
            4,
            new DateTime(2026, 7, 3),
            new DateTime(1970, 1, 1),
            1.005m,
            "Bills",
            "",
            false,
            false,
            false);

        var saved = Assert.Single(await db.Budgets.Where(b => b.UserId == 42).ToListAsync());
        Assert.Equal(1.01m, saved.Amount);

        var dateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveBudgetAsync(42, -1, "Bad", 1, 4, DateTime.MinValue, new DateTime(1970, 1, 1), 1m, "Bills", "", false, false, false));
        Assert.Equal("Next due date is required.", dateException.Message);
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
        Assert.Equal(100.13m, created.BeginningBalance);
        Assert.Equal(100.13m, created.ClearedBalance);
        Assert.Equal(200.13m, created.CreditLimit);
        Assert.Equal(3.46m, created.InterestRate);

        await service.SaveAccountAsync(99, created.AccountId, "Nope", "", 1, 1m, 0m, 0m, 1, 0m, 0m, "", DateTime.UtcNow);
        Assert.Equal("Checking", (await db.Accounts.FindAsync(created.AccountId))!.AccountName);

        var amountException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAccountAsync(42, created.AccountId, "Checking", "123", 1, -1m, 0m, 0m, 1, 0m, 0m, "", DateTime.UtcNow));
        Assert.Equal(CurrencyPolicy.NonNegativeAmountMessage, amountException.Message);

        await service.DeleteAccountAsync(99, created.AccountId);
        Assert.NotNull(await db.Accounts.FindAsync(created.AccountId));

        await service.DeleteAccountAsync(42, created.AccountId);
        Assert.Null(await db.Accounts.FindAsync(created.AccountId));
    }

    [Fact]
    public async Task AccountBalanceEdits_RebaseDefaultLedgerWithoutAddingOtherAccountBalances()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        db.Accounts.AddRange(
            new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 100m, Balance = 75m, ClearedBalance = 100m, IsDefault = true, UserId = 42 },
            new Account { AccountId = 2, AccountName = "Savings", AccountTypeId = 1, BeginningBalance = 400m, Balance = 400m, ClearedBalance = 400m, UserId = 42 });
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Food", UserId = 42 });
        db.Payees.Add(new Payee { PayeeId = 1, PayeeName = "Market", UserId = 42 });
        db.Transactions.AddRange(
            new Transaction { TransactionId = 1, UserId = 42, AccountId = 1, CategoryId = 1, PayeeId = 1, TransactionDate = new DateOnly(2026, 7, 1), Amount = -25m, Cleared = true },
            new Transaction { TransactionId = 2, UserId = 42, AccountId = 1, CategoryId = 1, PayeeId = 1, TransactionDate = new DateOnly(2026, 7, 2), Amount = -10m, Cleared = false });
        await db.SaveChangesAsync();

        await new AccountsDataService(db).SaveAccountAsync(42, 1, "Checking", "", 1, 250m, 0m, 0m, 1, 0m, 0m, "", DateTime.UtcNow);

        var checking = await db.Accounts.FindAsync(1);
        Assert.NotNull(checking);
        Assert.Equal(285m, checking.BeginningBalance);
        Assert.Equal(260m, checking.ClearedBalance);
        var checkbook = new CheckbookDataService(db);
        Assert.Equal(250m, await checkbook.GetCurrentBalanceAsync(42));
        Assert.Equal(260m, await checkbook.GetClearedBalanceAsync(42));
        Assert.Equal(250m, (await new ReportsDataService(db).GetCashflowAsync(42, 30, new DateOnly(2026, 7, 2))).StartingBalance);
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

        var amountException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveTransactionAsync(42, created.TransactionId, new DateOnly(2026, 7, 3), "Power", "Utilities", 10_000_000m, true, null, null));
        Assert.Equal(CurrencyPolicy.AmountTooLargeMessage, amountException.Message);

        var dateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveTransactionAsync(42, created.TransactionId, DateOnly.MinValue, "Power", "Utilities", 1m, true, null, null));
        Assert.Equal("Transaction date is required.", dateException.Message);

        await service.DeleteTransactionAsync(99, created.TransactionId);
        Assert.NotNull(await db.Transactions.FindAsync(created.TransactionId));

        await service.DeleteTransactionAsync(42, created.TransactionId);
        Assert.Null(await db.Transactions.FindAsync(created.TransactionId));
    }

    [Fact]
    public async Task DashboardSnapshot_UsesOnlyRequestedUserData()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        db.Accounts.AddRange(
            new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 100m, UserId = 42 },
            new Account { AccountId = 2, AccountName = "Other Checking", AccountTypeId = 1, BeginningBalance = 1000m, UserId = 99 });
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
            Budget(1, 42, "Electric", 1, 1, new DateTime(2026, 7, 4), 30m, frequencyId: 4, isBill: true),
            Budget(2, 99, "Other Electric", 1, 2, new DateTime(2026, 7, 4), 999m, frequencyId: 4, isBill: true));
        await db.SaveChangesAsync();

        var service = new DashboardDataService(new CheckbookDataService(db), new BudgetScheduleService(db));

        var snapshot = await service.GetSnapshotAsync(42, new DateTime(2026, 7, 4));

        Assert.Equal(75m, snapshot.TodayBalance);
        var bill = Assert.Single(snapshot.UpcomingBills);
        Assert.Equal("Electric", bill.Name);
        Assert.Equal(30m, snapshot.UpcomingBillsTotal);
        var category = Assert.Single(snapshot.CategorySpend);
        Assert.Equal("Utilities", category.CategoryName);
        Assert.Equal(25m, category.Total);
        Assert.Equal(-135m, snapshot.LowestProjectedBalance);
    }

    [Fact]
    public async Task BudgetScheduleService_CreateEditedNextOccurrence_RoundsAndValidatesAmountAndDate()
    {
        await using var db = CreateDbContext();
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Bills", UserId = 42 });
        db.Budgets.Add(Budget(1, 42, "Electric", 1, 1, new DateTime(2026, 7, 2), 25m, frequencyId: 1, endDate: new DateTime(2026, 7, 30)));
        await db.SaveChangesAsync();
        var service = new BudgetScheduleService(db);

        await service.CreateEditedNextOccurrenceAsync(42, 1, "Electric special", new DateTime(2026, 7, 2), 12.345m, "Bills", false, false);

        var editedNext = await db.Budgets.SingleAsync(b => b.BudgetName == "Electric special");
        Assert.Equal(12.35m, editedNext.Amount);
        Assert.Equal(new DateTime(2026, 7, 9), (await db.Budgets.FindAsync(1))!.NextDueDate);

        var amountException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateEditedNextOccurrenceAsync(42, 1, "Bad", new DateTime(2026, 7, 9), -1m, "Bills", false, false));
        Assert.Equal(CurrencyPolicy.NonNegativeAmountMessage, amountException.Message);

        var dateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateEditedNextOccurrenceAsync(42, 1, "Bad", DateTime.MinValue, 1m, "Bills", false, false));
        Assert.Equal("Due date is required.", dateException.Message);
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

    [Fact]
    public void AuthenticatedDataPages_ResolveUserThroughCurrentUserContext()
    {
        var targetPages = new[]
        {
            "Components/Pages/Home.razor.cs",
            "Components/Pages/Budget.razor.cs",
            "Components/Pages/Checkbook.razor.cs",
            "Components/Pages/Accounts.razor.cs",
            "Components/Pages/BudgetItems.razor.cs",
            "Components/Pages/Payees.razor.cs",
            "Components/Pages/CategoryBudgets.razor.cs",
            "Components/Pages/Reports.razor.cs"
        };

        foreach (var page in targetPages)
        {
            var source = ReadRepoFile(page);
            Assert.Contains("CurrentUserContext", source);
            Assert.Contains("CurrentUser.UserId", source);
            Assert.DoesNotContain("DefaultUserId", source);
            Assert.DoesNotContain("AuthService.CurrentUser.UserId > 0", source);
        }
    }

    [Fact]
    public void AuthService_InitializesThroughSerializedGate()
    {
        var source = ReadRepoFile("Services/AuthService.cs");

        Assert.Contains("SemaphoreSlim _initializeGate", source);
        Assert.Contains("await _initializeGate.WaitAsync()", source);
        Assert.Contains("_initializeGate.Release()", source);
        Assert.Contains("if (_isInitialized) return;", source);
    }

    [Fact]
    public void DashboardDataService_UsesIsolatedScopeForProductionSnapshotLoads()
    {
        var source = ReadRepoFile("Services/DashboardDataService.cs");

        Assert.Contains("IServiceScopeFactory", source);
        Assert.Contains("CreateAsyncScope()", source);
        Assert.Contains("GetRequiredService<CheckbookDataService>()", source);
        Assert.Contains("GetRequiredService<BudgetScheduleService>()", source);
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
        DateTime? endDate = null,
        bool isBill = false) => new()
        {
            BudgetId = id,
            UserId = userId,
            BudgetName = name,
            BudgetTypeId = budgetTypeId,
            CategoryId = categoryId,
            FrequencyId = frequencyId,
            NextDueDate = dueDate,
            EndDate = endDate ?? new DateTime(1970, 1, 1),
            Amount = amount,
            IsBill = isBill
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
