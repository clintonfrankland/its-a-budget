using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class TransactionRulesDataServiceTests
{
    [Fact]
    public void Matches_OptionalFiltersAndSignedBoundariesAreInclusive()
    {
        var unrestricted = Rule();
        Assert.True(TransactionRulesDataService.Matches(unrestricted, null, -500m, "Market", null));

        var debit = Rule();
        debit.AccountId = 2;
        debit.MinimumAmount = -100m;
        debit.MaximumAmount = -1m;
        Assert.True(TransactionRulesDataService.Matches(debit, 2, -100m, "Market", null));
        Assert.True(TransactionRulesDataService.Matches(debit, 2, -1m, null, "MARKET purchase"));
        Assert.False(TransactionRulesDataService.Matches(debit, 2, -100.01m, "Market", null));
        Assert.False(TransactionRulesDataService.Matches(debit, 2, 1m, "Market", null));
        Assert.False(TransactionRulesDataService.Matches(debit, 3, -25m, "Market", null));
        debit.IsEnabled = false;
        Assert.False(TransactionRulesDataService.Matches(debit, 2, -25m, "Market", null));
    }

    [Fact]
    public async Task SuggestAndReorder_UseEnabledPriorityAndUserIsolation()
    {
        await using var db = Db();
        db.TransactionRules.AddRange(
            Rule(1, "First", 0), Rule(1, "Second", 1), Rule(2, "Other", 0));
        await db.SaveChangesAsync();
        var service = new TransactionRulesDataService(db);

        Assert.Equal("First", (await service.SuggestAsync(1, null, -1m, "Store", null))!.PayeeName);
        var ids = (await service.GetRulesAsync(1)).Select(rule => rule.TransactionRuleId).Reverse().ToArray();
        await service.ReorderAsync(1, ids);
        Assert.Equal("Second", (await service.SuggestAsync(1, null, -1m, "Store", null))!.PayeeName);
        Assert.Null(await service.SuggestAsync(3, null, -1m, "Store", null));
    }

    [Fact]
    public async Task Save_RejectsAnotherUsersAccountCategoryAndPayee()
    {
        await using var db = Db();
        db.Accounts.AddRange(Account(1, 1), Account(2, 2));
        db.Categories.Add(new Category { CategoryId = 2, CategoryName = "Other category", UserId = 2 });
        db.Payees.Add(new Payee { PayeeId = 2, PayeeName = "Other payee", UserId = 2 });
        await db.SaveChangesAsync();
        var service = new TransactionRulesDataService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(1, new(null, "x", 2, null, null, null, null, "note", true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(1, new(null, "x", null, null, null, "Other category", null, null, true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(1, new(null, "x", null, null, null, null, "Other payee", null, true)));
    }

    [Fact]
    public async Task Preview_IncludesOnlyRealChangesAndEveryChangedField()
    {
        await using var db = Db();
        SeedOutputs(db);
        db.TransactionRules.Add(Rule(1, "New Payee", 0, "Food", "new note"));
        db.Transactions.AddRange(
            Tx(1, 1, 1, 1, "old note"),
            Tx(2, 1, 2, 2, "new note"),
            Tx(3, 2, 1, 1, "old note"));
        await db.SaveChangesAsync();

        var preview = await new TransactionRulesDataService(db).PreviewAsync(1);

        var item = Assert.Single(preview);
        Assert.Equal(1, item.TransactionId);
        Assert.Equal(["Category", "Payee", "Notes"], item.ChangedFields);
    }

    [Fact]
    public async Task Apply_CountsOnlyRealChangesAndNeverUpdatesAnotherUser()
    {
        await using var db = Db();
        SeedOutputs(db);
        db.TransactionRules.Add(Rule(1, "New Payee", 0, "Food", "new note"));
        db.Transactions.AddRange(Tx(1, 1, 1, 1, "old"), Tx(2, 1, 2, 2, "new note"), Tx(3, 2, 1, 1, "old"));
        await db.SaveChangesAsync();
        var service = new TransactionRulesDataService(db);

        Assert.Equal(1, await service.ApplyAsync(1, [1, 2, 3]));
        Assert.Equal("new note", (await db.Transactions.FindAsync(1))!.Notes);
        Assert.Equal("old", (await db.Transactions.FindAsync(3))!.Notes);
    }

    [Fact]
    public void Checkbook_CreateAndEditUseAutomaticReviewThenAllowOverride()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Components", "Pages", "Checkbook.razor.cs"));
        Assert.Contains("if (!ruleSuggestionReviewed && await ReviewMatchingRuleAsync())", source);
        Assert.Contains("editTransactionId = -1;", source);
        Assert.Contains("editTransactionId = transactionId;", source);
        Assert.True(source.Split("ruleSuggestionReviewed = false;", StringSplitOptions.None).Length >= 4);
        Assert.Contains("Review or override", source);
    }

    [Fact]
    public async Task Apply_RollsBackAllUpdatesWhenBulkSaveFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseSqlite(connection).Options;
        await using var db = new ClintonFranklandDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        SeedOutputs(db);
        db.TransactionRules.Add(Rule(1, "New Payee", 0, notes: "FAIL"));
        db.Transactions.AddRange(Tx(1, 1, 1, 1, "old one"), Tx(2, 1, 1, 1, "old two"));
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_rule_apply BEFORE UPDATE ON cfTransactions WHEN NEW.Notes = 'FAIL' BEGIN SELECT RAISE(ABORT, 'forced bulk failure'); END;");

        await Assert.ThrowsAnyAsync<Exception>(() => new TransactionRulesDataService(db).ApplyAsync(1, [1, 2]));
        db.ChangeTracker.Clear();
        Assert.Equal(new string?[] { "old one", "old two" }, await db.Transactions.OrderBy(transaction => transaction.TransactionId).Select(transaction => transaction.Notes).ToArrayAsync());
    }

    [Fact]
    public void Matches_PrefersStableMerchantEntityIdAndNormalizesAliases()
    {
        var rule = Rule();
        rule.MerchantEntityId = "merchant-123";
        Assert.True(TransactionRulesDataService.Matches(rule, 1, -10m, "anything", null, "merchant-123"));
        Assert.False(TransactionRulesDataService.Matches(rule, 1, -10m, "market", null, "merchant-456"));
        Assert.Equal("COFFEE SHOP", TransactionRulesDataService.NormalizeMerchant("Coffee-Shop #0421"));
    }

    [Fact]
    public async Task LearnedProposals_RequireDominanceAndRemainUserLocal()
    {
        await using var db = Db();
        SeedOutputs(db);
        db.Accounts.AddRange(Account(1, 1), Account(2, 2));
        for (var index = 1; index <= 3; index++)
        {
            db.Transactions.Add(new Transaction { TransactionId = index, UserId = 1, AccountId = 1, PayeeId = 2, CategoryId = 2, Amount = -5m, Cleared = true, TransactionDate = new DateOnly(2026, 1, index) });
            db.PlaidTransactionStaging.Add(new PlaidTransactionStaging { UserId = 1, BudgetAccountId = 1, PlaidItemId = 1, PlaidTransactionId = $"tx-{index}", PlaidAccountId = "account", MerchantEntityId = "coffee", MerchantName = "Coffee Shop", ReviewState = PlaidReconciliationReviewState.Confirmed, LinkedTransactionId = index, TransactionDate = new DateOnly(2026, 1, index), FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow });
        }
        db.Transactions.Add(new Transaction { TransactionId = 9, UserId = 2, AccountId = 2, PayeeId = 2, CategoryId = 2, Amount = -5m, Cleared = true, TransactionDate = new DateOnly(2026, 1, 9) });
        db.PlaidTransactionStaging.Add(new PlaidTransactionStaging { UserId = 2, BudgetAccountId = 2, PlaidItemId = 2, PlaidTransactionId = "other", PlaidAccountId = "other", MerchantEntityId = "coffee", MerchantName = "Coffee Shop", ReviewState = PlaidReconciliationReviewState.Confirmed, LinkedTransactionId = 9, TransactionDate = new DateOnly(2026, 1, 9), FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var proposals = await new TransactionRulesDataService(db).GetLearnedProposalsAsync(1);
        var proposal = Assert.Single(proposals);
        Assert.Equal(3, proposal.MatchCount);
        Assert.Equal("coffee", proposal.MerchantEntityId);
        await new TransactionRulesDataService(db).SaveLearnedProposalAsync(1, proposal);
        Assert.Equal(TransactionRuleSource.Learned, Assert.Single(db.TransactionRules).Source);
    }

    private static TransactionRule Rule() => new() { IsEnabled = true, ContainsText = "market", ApprovalState = TransactionRuleApprovalState.Approved };
    private static TransactionRule Rule(int userId, string payee, int priority, string? category = null, string? notes = null) => new() { UserId = userId, ContainsText = "store", PayeeName = payee, CategoryName = category, Notes = notes, Priority = priority, IsEnabled = true };
    private static Account Account(int id, int userId) => new() { AccountId = id, UserId = userId, AccountName = $"Account {id}", AccountTypeId = 1 };
    private static ClintonFranklandDbContext Db() => new(new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Transaction Tx(int id, int user, int payee, int category, string notes) => new() { TransactionId = id, UserId = user, PayeeId = payee, CategoryId = category, AccountId = 1, Amount = -10m, Notes = notes, TransactionDate = new DateOnly(2026, 1, 1) };
    private static void SeedOutputs(ClintonFranklandDbContext db)
    {
        db.Categories.AddRange(new Category { CategoryId = 1, CategoryName = "Old", UserId = 1 }, new Category { CategoryId = 2, CategoryName = "Food", UserId = 1 });
        db.Payees.AddRange(new Payee { PayeeId = 1, PayeeName = "New Payee store", UserId = 1 }, new Payee { PayeeId = 2, PayeeName = "New Payee", UserId = 1 });
    }
}
