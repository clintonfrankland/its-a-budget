using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidTransactionSyncServiceTests
{
    [Fact]
    public async Task MutationDuringPagination_RestartsAtOriginalCursor_AndCommitsOnlyStableLoop()
    {
        await using var db = new ClintonFranklandDbContext(new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var provider = DataProtectionProvider.Create("sync-test");
        var protector = provider.CreateProtector("BudgetApp.Plaid.AccessToken.v1");
        db.PlaidItems.Add(new PlaidItem { PlaidItemId = 1, UserId = 7, ItemId = "item", EncryptedAccessToken = protector.Protect("token"), Status = PlaidItemStatus.Active, TransactionsCursor = "original" });
        db.PlaidAccountMappings.Add(new PlaidAccountMapping { PlaidItemId = 1, PlaidAccountId = "account", BudgetAccountId = 3 });
        await db.SaveChangesAsync();
        var client = new RestartingClient();
        var service = new PlaidTransactionSyncService(db, client, provider);

        await service.SyncItemAsync(1, CancellationToken.None);

        Assert.Equal(["original", "partial", "original"], client.Cursors);
        Assert.Equal("complete", (await db.PlaidItems.SingleAsync()).TransactionsCursor);
        var staged = await db.PlaidTransactionStaging.SingleAsync();
        Assert.Equal("stable-transaction", staged.PlaidTransactionId);
        Assert.False(staged.IsPending);
        Assert.Empty(db.Transactions);
    }

    [Fact]
    public async Task FailedPagination_DoesNotPersistPartialStagingOrAdvanceCursor()
    {
        await using var db = new ClintonFranklandDbContext(new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var provider = DataProtectionProvider.Create("failed-sync-test");
        var protector = provider.CreateProtector("BudgetApp.Plaid.AccessToken.v1");
        db.PlaidItems.Add(new PlaidItem { PlaidItemId = 1, UserId = 7, ItemId = "item", EncryptedAccessToken = protector.Protect("token"), Status = PlaidItemStatus.Active, TransactionsCursor = "original" });
        db.PlaidAccountMappings.Add(new PlaidAccountMapping { PlaidItemId = 1, PlaidAccountId = "account", BudgetAccountId = 3 });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new PlaidTransactionSyncService(db, new FailingClient(), provider).SyncItemAsync(1, CancellationToken.None));

        Assert.Equal("original", (await db.PlaidItems.SingleAsync()).TransactionsCursor);
        Assert.Empty(await db.PlaidTransactionStaging.ToListAsync());
        Assert.Equal("failed", (await db.PlaidSyncRuns.SingleAsync()).Status);
    }

    [Fact]
    public async Task AddedModifiedRemoved_AndPendingToPostedLifecycle_IsIdempotentAndCaseSensitive()
    {
        await using var db = CreateDatabase();
        var provider = SeedItemAndMapping(db, itemId: 1, userId: 7, plaidItemId: "item-one", accountId: "account", budgetAccountId: 3);
        var service = new PlaidTransactionSyncService(db, new SequencedClient(
            new PlaidSyncPage([Tx("CaseSensitive_Id", pending: true, pendingId: null, amount: 10m)], [], [], "one", false),
            new PlaidSyncPage([], [Tx("CaseSensitive_Id", pending: false, pendingId: "pending-id", amount: 11m), Tx("casesensitive_id", pending: false, pendingId: null, amount: 12m)], [], "two", false),
            new PlaidSyncPage([], [], ["CaseSensitive_Id"], "three", false)), provider);

        await service.SyncItemAsync(1, CancellationToken.None);
        await service.SyncItemAsync(1, CancellationToken.None);
        await service.SyncItemAsync(1, CancellationToken.None);

        var staged = await db.PlaidTransactionStaging.OrderBy(x => x.PlaidTransactionId).ToListAsync();
        Assert.Equal(2, staged.Count);
        var posted = Assert.Single(staged, x => x.PlaidTransactionId == "CaseSensitive_Id");
        Assert.True(posted.IsRemoved);
        Assert.False(posted.IsPending);
        Assert.Equal("pending-id", posted.PendingTransactionId);
        Assert.Equal(11m, posted.PlaidAmount);
        Assert.Contains(staged, x => x.PlaidTransactionId == "casesensitive_id" && !x.IsRemoved);
        Assert.Empty(db.Transactions);
    }

    [Fact]
    public async Task RemovedPendingPredecessor_ProducesPendingToPostedReconciliationEvidence()
    {
        await using var db = CreateDatabase();
        var provider = SeedItemAndMapping(db, itemId: 1, userId: 7, plaidItemId: "item-one", accountId: "account", budgetAccountId: 3);
        db.Accounts.Add(new Account { AccountId = 3, AccountName = "Checking", UserId = 7 });
        db.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            UserId = 7,
            AccountId = 3,
            PayeeId = 1,
            Payee = new Payee { PayeeId = 1, UserId = 7, PayeeName = "Test" },
            CategoryId = 1,
            TransactionDate = new DateOnly(2026, 7, 27),
            Amount = -10m
        });
        await db.SaveChangesAsync();

        var service = new PlaidTransactionSyncService(db, new SequencedClient(
            new PlaidSyncPage([Tx("pending-id", pending: true, pendingId: null, amount: 10m)], [], [], "one", false),
            new PlaidSyncPage([Tx("posted-id", pending: false, pendingId: "pending-id", amount: 10m)], [], ["pending-id"], "two", false)), provider);

        await service.SyncItemAsync(1, CancellationToken.None);
        await service.SyncItemAsync(1, CancellationToken.None);

        db.ChangeTracker.Clear();
        var postedStagingId = db.PlaidTransactionStaging.Single(transaction => transaction.PlaidTransactionId == "posted-id").PlaidTransactionStagingId;
        var posted = Assert.Single(await new PlaidReconciliationService(db).ReconcileAsync(7, CancellationToken.None),
            result => result.PlaidTransactionStagingId == postedStagingId);

        Assert.Single(posted.Candidates);
        Assert.Equal(PlaidReconciliationDisposition.HighConfidence, posted.Disposition);
        Assert.Contains("pending-to-posted-relationship", Assert.Single(posted.Candidates).Evidence);
        Assert.True((await db.PlaidTransactionStaging.SingleAsync(transaction => transaction.PlaidTransactionId == "pending-id")).IsRemoved);
    }

    [Fact]
    public async Task Sync_ScopesEvidenceToMappedAccountAndOwningItemUser()
    {
        await using var db = CreateDatabase();
        var provider = SeedItemAndMapping(db, 1, 7, "item-one", "approved", 3);
        SeedItemAndMapping(db, 2, 8, "item-two", "approved", 4, provider);
        await db.SaveChangesAsync();
        var service = new PlaidTransactionSyncService(db, new PerItemClient(), provider);

        await service.SyncItemAsync(1, CancellationToken.None);
        await service.SyncItemAsync(2, CancellationToken.None);

        var staged = await db.PlaidTransactionStaging.OrderBy(x => x.PlaidItemId).ToListAsync();
        Assert.Equal(2, staged.Count);
        Assert.Collection(staged,
            x => { Assert.Equal(1, x.PlaidItemId); Assert.Equal(7, x.UserId); Assert.Equal(3, x.BudgetAccountId); Assert.Equal("shared-id", x.PlaidTransactionId); },
            x => { Assert.Equal(2, x.PlaidItemId); Assert.Equal(8, x.UserId); Assert.Equal(4, x.BudgetAccountId); Assert.Equal("shared-id", x.PlaidTransactionId); });
    }

    private static ClintonFranklandDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ClintonFranklandDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IDataProtectionProvider SeedItemAndMapping(ClintonFranklandDbContext db, int itemId, int userId, string plaidItemId,
        string accountId, int budgetAccountId, IDataProtectionProvider? provider = null)
    {
        provider ??= DataProtectionProvider.Create($"plaid-lifecycle-{Guid.NewGuid()}");
        var protector = provider.CreateProtector("BudgetApp.Plaid.AccessToken.v1");
        db.PlaidItems.Add(new PlaidItem { PlaidItemId = itemId, UserId = userId, ItemId = plaidItemId, EncryptedAccessToken = protector.Protect("token"), Status = PlaidItemStatus.Active });
        db.PlaidAccountMappings.Add(new PlaidAccountMapping { PlaidItemId = itemId, PlaidAccountId = accountId, BudgetAccountId = budgetAccountId });
        db.SaveChanges();
        return provider;
    }

    private static PlaidSyncTransaction Tx(string id, bool pending, string? pendingId, decimal amount) =>
        new(id, "account", amount, "USD", new DateOnly(2026, 7, 27), pending, pendingId, null, null, "Test");

    private sealed class RestartingClient : IPlaidClient
    {
        public List<string?> Cursors { get; } = [];
        public Task<PlaidSyncPage> SyncTransactionsAsync(string token, string? cursor, CancellationToken ct)
        {
            Cursors.Add(cursor);
            if (Cursors.Count == 1) return Task.FromResult(new PlaidSyncPage([Tx("discarded")], [], [], "partial", true));
            if (Cursors.Count == 2) throw new PlaidSyncMutationDuringPaginationException();
            return Task.FromResult(new PlaidSyncPage([Tx("stable-transaction")], [], [], "complete", false));
        }
        private static PlaidSyncTransaction Tx(string id) => new(id, "account", 12.34m, "USD", new DateOnly(2026, 7, 27), false, null, null, null, "Test");
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int u, bool a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string p, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken c) => throw new NotSupportedException();
        public Task RemoveItemAsync(string a, CancellationToken c) => throw new NotSupportedException();
    }

    private sealed class FailingClient : IPlaidClient
    {
        private int calls;
        public Task<PlaidSyncPage> SyncTransactionsAsync(string token, string? cursor, CancellationToken ct) => ++calls == 1
            ? Task.FromResult(new PlaidSyncPage([new PlaidSyncTransaction("partial", "account", 1m, "USD", new DateOnly(2026, 7, 27), false, null, null, null, "partial")], [], [], "next", true))
            : throw new InvalidOperationException("network failure");
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int u, bool a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string p, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken c) => throw new NotSupportedException();
        public Task RemoveItemAsync(string a, CancellationToken c) => throw new NotSupportedException();
    }

    private sealed class SequencedClient(params PlaidSyncPage[] pages) : IPlaidClient
    {
        private int index;
        public Task<PlaidSyncPage> SyncTransactionsAsync(string token, string? cursor, CancellationToken ct) => Task.FromResult(pages[index++]);
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int u, bool a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string p, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken c) => throw new NotSupportedException();
        public Task RemoveItemAsync(string a, CancellationToken c) => throw new NotSupportedException();
    }

    private sealed class PerItemClient : IPlaidClient
    {
        public Task<PlaidSyncPage> SyncTransactionsAsync(string token, string? cursor, CancellationToken ct) => Task.FromResult(
            new PlaidSyncPage([new PlaidSyncTransaction("shared-id", "approved", 1m, "USD", new DateOnly(2026, 7, 27), false, null, null, null, "Mapped")], [], [], "done", false));
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int u, bool a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string p, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken c) => throw new NotSupportedException();
        public Task RemoveItemAsync(string a, CancellationToken c) => throw new NotSupportedException();
    }
}
