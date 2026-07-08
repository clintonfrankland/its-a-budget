using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class RecurringPreviewTests
{
    [Fact]
    public async Task GetRecurringPreviewAsync_GroupsUpcomingOccurrencesAndScopesToUser()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Budgets.AddRange(
            Budget(1, 42, "Paycheck", 0, 1, new DateTime(2026, 7, 10), 1000m, frequencyId: 2),
            Budget(2, 42, "Electric", 1, 2, new DateTime(2026, 7, 10), 120m, frequencyId: 4, isBill: true, payeeId: 1),
            Budget(3, 42, "Water", 1, 2, new DateTime(2026, 7, 18), 40m, frequencyId: 1, isBill: true, payeeId: 1),
            Budget(4, 99, "Other User", 1, 3, new DateTime(2026, 7, 10), 999m, frequencyId: 1));
        await db.SaveChangesAsync();

        var preview = await new BudgetScheduleService(db).GetRecurringPreviewAsync(42, new DateTime(2026, 7, 10), 9);

        Assert.Equal(["Paycheck", "Electric", "Water"], preview.Select(i => i.BudgetName));
        Assert.Equal([new DateTime(2026, 7, 10), new DateTime(2026, 7, 10), new DateTime(2026, 7, 18)], preview.Select(i => i.DueDate));
        Assert.Equal([1000m, -120m, -40m], preview.Select(i => i.Amount));
        Assert.DoesNotContain(preview, i => i.BudgetName == "Other User");
        Assert.Equal(2, preview.GroupBy(i => i.DueDate.Date).Count());
        Assert.All(preview, i => Assert.True(i.CanRecordToCheckbook));
    }

    [Fact]
    public async Task GetRecurringPreviewAsync_RollsOverdueItemsForwardToWindowStart()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Budgets.AddRange(
            Budget(1, 42, "Weekly Electric", 1, 2, new DateTime(2026, 7, 1), 120m, frequencyId: 1, isBill: true, payeeId: 1),
            Budget(2, 42, "One Time Old Bill", 1, 2, new DateTime(2026, 7, 1), 60m, frequencyId: 0, isBill: true, payeeId: 1));
        await db.SaveChangesAsync();

        var preview = await new BudgetScheduleService(db).GetRecurringPreviewAsync(42, new DateTime(2026, 7, 10), 14);

        Assert.Equal([new DateTime(2026, 7, 15), new DateTime(2026, 7, 22)], preview.Select(i => i.DueDate));
        Assert.All(preview, i => Assert.Equal("Weekly Electric", i.BudgetName));
        Assert.DoesNotContain(preview, i => i.DueDate < new DateTime(2026, 7, 10));
    }

    [Fact]
    public async Task RecordOccurrenceToCheckbookAsync_CreatesTransactionAndAdvancesOnce()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Budgets.Add(Budget(1, 42, "Electric", 1, 2, new DateTime(2026, 7, 10), 120.345m, frequencyId: 4, isBill: true, payeeId: 1));
        await db.SaveChangesAsync();

        var service = new BudgetScheduleService(db);
        var recorded = await service.RecordOccurrenceToCheckbookAsync(42, 1, new DateTime(2026, 7, 10));
        var repeated = await service.RecordOccurrenceToCheckbookAsync(42, 1, new DateTime(2026, 7, 10));

        Assert.True(recorded);
        Assert.False(repeated);
        var transaction = Assert.Single(await db.Transactions.ToListAsync());
        Assert.Equal(new DateOnly(2026, 7, 10), transaction.TransactionDate);
        Assert.Equal(-120.35m, transaction.Amount);
        Assert.Equal(1, transaction.PayeeId);
        Assert.Equal(2, transaction.CategoryId);
        Assert.Equal(1, transaction.AccountId);
        Assert.False(transaction.Cleared);
        Assert.Contains("Electric", transaction.Notes);
        Assert.Equal(new DateTime(2026, 8, 10), (await db.Budgets.FindAsync(1))!.NextDueDate);
    }

    [Fact]
    public async Task RecordOccurrenceToCheckbookAsync_HandlesLaterProjectedOccurrenceOnce()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Budgets.Add(Budget(1, 42, "Electric", 1, 2, new DateTime(2026, 7, 10), 120m, frequencyId: 1, isBill: true, payeeId: 1));
        await db.SaveChangesAsync();

        var service = new BudgetScheduleService(db);
        var recorded = await service.RecordOccurrenceToCheckbookAsync(42, 1, new DateTime(2026, 7, 24));
        var repeated = await service.RecordOccurrenceToCheckbookAsync(42, 1, new DateTime(2026, 7, 24));

        Assert.True(recorded);
        Assert.False(repeated);
        var transaction = Assert.Single(await db.Transactions.ToListAsync());
        Assert.Equal(new DateOnly(2026, 7, 24), transaction.TransactionDate);
        Assert.Equal(-120m, transaction.Amount);
        Assert.Equal(new DateTime(2026, 7, 31), (await db.Budgets.FindAsync(1))!.NextDueDate);
    }

    [Fact]
    public async Task RecordOccurrenceToCheckbookAsync_UsesAutopayClearedAndIncomeSign()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Budgets.Add(Budget(1, 42, "Payroll", 0, 1, new DateTime(2026, 7, 10), 500m, frequencyId: 2, isAutomatic: true));
        await db.SaveChangesAsync();

        await new BudgetScheduleService(db).RecordOccurrenceToCheckbookAsync(42, 1, new DateTime(2026, 7, 10));

        var transaction = Assert.Single(await db.Transactions.ToListAsync());
        Assert.Equal(500m, transaction.Amount);
        Assert.True(transaction.Cleared);
        Assert.Equal("Payroll", (await db.Payees.FindAsync(transaction.PayeeId))!.PayeeName);
    }

    [Fact]
    public async Task RecordOccurrenceToCheckbookAsync_UsesSharedBudgetScopeAndAccount()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 10, Name = "Household", OwnerUserId = 99, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        db.BudgetMembers.Add(new BudgetMember { BudgetMemberId = 1, SharedBudgetId = 10, UserId = 42, Role = BudgetMemberRole.Editor, Status = BudgetMemberStatus.Active, CreatedAtUtc = DateTime.UtcNow });
        db.Categories.Add(new Category { CategoryId = 10, CategoryName = "Shared Utilities", UserId = 99, SharedBudgetId = 10 });
        db.Accounts.Add(new Account { AccountId = 10, AccountName = "Shared Checking", AccountTypeId = 1, BeginningBalance = 0m, UserId = 99, SharedBudgetId = 10, IsDefault = true });
        db.Budgets.Add(new Budget
        {
            BudgetId = 10,
            UserId = 99,
            SharedBudgetId = 10,
            BudgetName = "Shared Electric",
            BudgetTypeId = 1,
            CategoryId = 10,
            FrequencyId = 4,
            NextDueDate = new DateTime(2026, 7, 10),
            EndDate = new DateTime(1970, 1, 1),
            Amount = 75m,
            IsBill = true
        });
        await db.SaveChangesAsync();

        var service = new BudgetScheduleService(db);
        var preview = await service.GetRecurringPreviewAsync(42, new DateTime(2026, 7, 10), 1);
        var item = Assert.Single(preview, i => i.BudgetName == "Shared Electric");

        Assert.True(item.CanRecordToCheckbook);
        await service.RecordOccurrenceToCheckbookAsync(42, item.BudgetId, item.DueDate);

        var transaction = Assert.Single(await db.Transactions.Where(t => t.SharedBudgetId == 10).ToListAsync());
        Assert.Equal(42, transaction.UserId);
        Assert.Equal(10, transaction.AccountId);
        Assert.Equal(10, transaction.CategoryId);
    }

    [Fact]
    public async Task SkipOccurrenceAsync_AdvancesOnceWithoutTransaction()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Budgets.Add(Budget(1, 42, "Electric", 1, 2, new DateTime(2026, 7, 10), 120m, frequencyId: 1, isBill: true));
        await db.SaveChangesAsync();

        var service = new BudgetScheduleService(db);
        var skipped = await service.SkipOccurrenceAsync(42, 1, new DateTime(2026, 7, 10));
        var repeated = await service.SkipOccurrenceAsync(42, 1, new DateTime(2026, 7, 10));

        Assert.True(skipped);
        Assert.False(repeated);
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Equal(new DateTime(2026, 7, 17), (await db.Budgets.FindAsync(1))!.NextDueDate);
    }

    [Fact]
    public async Task SkipOccurrenceAsync_HandlesLaterProjectedOccurrenceOnceWithoutTransaction()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Budgets.Add(Budget(1, 42, "Water", 1, 2, new DateTime(2026, 7, 10), 40m, frequencyId: 1, isBill: true));
        await db.SaveChangesAsync();

        var service = new BudgetScheduleService(db);
        var skipped = await service.SkipOccurrenceAsync(42, 1, new DateTime(2026, 7, 24));
        var repeated = await service.SkipOccurrenceAsync(42, 1, new DateTime(2026, 7, 24));

        Assert.True(skipped);
        Assert.False(repeated);
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Equal(new DateTime(2026, 7, 31), (await db.Budgets.FindAsync(1))!.NextDueDate);
    }

    [Fact]
    public async Task InvalidCategoriesWarnAndCannotRecord()
    {
        await using var db = CreateDbContext();
        SeedCoreData(db);
        db.Categories.Add(new Category { CategoryId = 4, CategoryName = " ", UserId = 42 });
        db.Budgets.AddRange(
            Budget(1, 42, "Blank Category", 1, 4, new DateTime(2026, 7, 10), 10m, frequencyId: 1),
            Budget(2, 42, "Wrong Category", 1, 3, new DateTime(2026, 7, 10), 10m, frequencyId: 1));
        await db.SaveChangesAsync();

        var service = new BudgetScheduleService(db);
        var preview = await service.GetRecurringPreviewAsync(42, new DateTime(2026, 7, 10), 1);
        var blankCategory = Assert.Single(preview, i => i.BudgetName == "Blank Category");
        var wrongCategory = Assert.Single(preview, i => i.BudgetName == "Wrong Category");

        Assert.All(preview, i =>
        {
            Assert.True(i.HasCategoryWarning);
            Assert.False(i.CanRecordToCheckbook);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordOccurrenceToCheckbookAsync(42, blankCategory.BudgetId, new DateTime(2026, 7, 10)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordOccurrenceToCheckbookAsync(42, wrongCategory.BudgetId, new DateTime(2026, 7, 10)));
        Assert.Empty(await db.Transactions.ToListAsync());

        var serviceSource = ReadRepoFile("Services/BudgetScheduleService.cs");
        Assert.Contains("Category is missing", serviceSource);
    }

    [Fact]
    public void BudgetItemsPage_WiresRecurringPreviewCalendarActions()
    {
        var source = ReadRepoFile("Components/Pages/BudgetItems.razor");
        var codeBehind = ReadRepoFile("Components/Pages/BudgetItems.razor.cs");

        Assert.Contains("Recurring Preview", source);
        Assert.Contains("calendar_month", source);
        Assert.Contains("RecordOccurrenceAsync", source);
        Assert.Contains("SkipOccurrenceAsync", source);
        Assert.Contains("HasCategoryWarning", source);
        Assert.Contains("ShowEditBudgetAsync(item.BudgetId)", source);
        Assert.Contains("GetRecurringPreviewAsync", codeBehind);
        Assert.Contains("RecordOccurrenceToCheckbookAsync", codeBehind);
        Assert.Contains("handlingOccurrenceKeys", codeBehind);
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static void SeedCoreData(ClintonFranklandDbContext db)
    {
        db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
        db.Accounts.Add(new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, BeginningBalance = 0m, UserId = 42, IsDefault = true });
        db.Categories.AddRange(
            new Category { CategoryId = 1, CategoryName = "Income", UserId = 42 },
            new Category { CategoryId = 2, CategoryName = "Utilities", UserId = 42 },
            new Category { CategoryId = 3, CategoryName = "Other Utilities", UserId = 99 });
        db.Frequencies.AddRange(
            new Frequency { FrequencyId = 1, FrequencyName = "Weekly", Sort = 1 },
            new Frequency { FrequencyId = 2, FrequencyName = "Bi-weekly", Sort = 2 },
            new Frequency { FrequencyId = 4, FrequencyName = "Monthly", Sort = 3 });
        db.Payees.Add(new Payee { PayeeId = 1, PayeeName = "Power", UserId = 42, IsDeleted = false });
    }

    private static Budget Budget(
        int id,
        int userId,
        string name,
        int budgetTypeId,
        int categoryId,
        DateTime dueDate,
        decimal amount,
        int frequencyId,
        bool isBill = false,
        int? payeeId = null,
        bool isAutomatic = false) => new()
        {
            BudgetId = id,
            UserId = userId,
            BudgetName = name,
            BudgetTypeId = budgetTypeId,
            CategoryId = categoryId,
            FrequencyId = frequencyId,
            NextDueDate = dueDate,
            EndDate = new DateTime(1970, 1, 1),
            Amount = amount,
            IsBill = isBill,
            IsAutomatic = isAutomatic,
            PayeeId = payeeId
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
