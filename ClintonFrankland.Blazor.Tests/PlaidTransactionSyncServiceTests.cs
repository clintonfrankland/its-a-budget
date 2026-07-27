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
        private static PlaidSyncTransaction Tx(string id) => new(id, "account", 12.34m, "USD", new DateOnly(2026, 7, 27), false, null, null, "Test");
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
            ? Task.FromResult(new PlaidSyncPage([new PlaidSyncTransaction("partial", "account", 1m, "USD", new DateOnly(2026, 7, 27), false, null, null, "partial")], [], [], "next", true))
            : throw new InvalidOperationException("network failure");
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int u, bool a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string p, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken c) => throw new NotSupportedException();
        public Task RemoveItemAsync(string a, CancellationToken c) => throw new NotSupportedException();
    }
}
