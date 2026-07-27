using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidConnectionServiceTests
{
    [Fact]
    public async Task Exchange_StoresOnlyProtectedAccessToken_AndDoesNotCreateTransactions()
    {
        await using var database = CreateDatabase();
        SeedUsersAndAccounts(database);
        var client = new FakePlaidClient();
        var provider = DataProtectionProvider.Create("PlaidConnectionServiceTests");
        var service = new PlaidConnectionService(database, new SharedBudgetDataService(database), client, provider);

        var result = await service.ExchangePublicTokenAsync(1, "public-token", "ins_1", "Sandbox Bank", CancellationToken.None);

        var item = await database.PlaidItems.SingleAsync();
        Assert.Equal(result.PlaidItemId, item.PlaidItemId);
        Assert.NotEqual(client.AccessToken, item.EncryptedAccessToken);
        Assert.DoesNotContain(client.AccessToken, item.EncryptedAccessToken, StringComparison.Ordinal);
        Assert.Empty(database.Transactions);
    }

    [Fact]
    public async Task Mapping_RejectsAccountOutsideUsersSharedBudgetAuthorization()
    {
        await using var database = CreateDatabase();
        SeedUsersAndAccounts(database);
        var provider = DataProtectionProvider.Create("PlaidMappingAuthorizationTests");
        var service = new PlaidConnectionService(database, new SharedBudgetDataService(database), new FakePlaidClient(), provider);
        var connection = await service.ExchangePublicTokenAsync(1, "public-token", null, null, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.MapAccountAsync(1, connection.PlaidItemId, "CaseSensitive_Id", 2, CancellationToken.None));

        Assert.Empty(database.PlaidAccountMappings);
        Assert.Empty(database.Transactions);
    }

    [Fact]
    public async Task Mapping_PreservesCaseSensitivePlaidAccountId()
    {
        await using var database = CreateDatabase();
        SeedUsersAndAccounts(database);
        var provider = DataProtectionProvider.Create("PlaidMappingCaseTests");
        var service = new PlaidConnectionService(database, new SharedBudgetDataService(database), new FakePlaidClient(), provider);
        var connection = await service.ExchangePublicTokenAsync(1, "public-token", null, null, CancellationToken.None);

        await service.MapAccountAsync(1, connection.PlaidItemId, "CaseSensitive_Id", 1, CancellationToken.None);

        Assert.Equal("CaseSensitive_Id", (await database.PlaidAccountMappings.SingleAsync()).PlaidAccountId);
        Assert.Empty(database.Transactions);
    }

    [Fact]
    public async Task FirstApprovedMapping_QueuesInitialTransactionsSync()
    {
        await using var database = CreateDatabase();
        SeedUsersAndAccounts(database);
        var provider = DataProtectionProvider.Create("PlaidInitialSyncTests");
        var service = new PlaidConnectionService(database, new SharedBudgetDataService(database), new FakePlaidClient(), provider);
        var connection = await service.ExchangePublicTokenAsync(1, "public-token", null, null, CancellationToken.None);

        await service.MapAccountAsync(1, connection.PlaidItemId, "CaseSensitive_Id", 1, CancellationToken.None);

        var delivery = Assert.Single(database.PlaidWebhookDeliveries);
        Assert.Equal("item-1", delivery.ItemId);
        Assert.Equal("INITIAL_TRANSACTIONS_SYNC", delivery.WebhookType);
        Assert.Equal("queued", delivery.Status);
    }

    [Fact]
    public async Task Mapping_RejectsAccountThatWasNotDiscoveredForThePlaidItem()
    {
        await using var database = CreateDatabase();
        SeedUsersAndAccounts(database);
        var provider = DataProtectionProvider.Create("PlaidMappingOwnershipTests");
        var service = new PlaidConnectionService(database, new SharedBudgetDataService(database), new FakePlaidClient(), provider);
        var connection = await service.ExchangePublicTokenAsync(1, "public-token", null, null, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MapAccountAsync(1, connection.PlaidItemId, "not-an-account-on-this-item", 1, CancellationToken.None));

        Assert.Contains("does not belong", exception.Message);
        Assert.Empty(database.PlaidAccountMappings);
    }

    [Fact]
    public async Task CompleteUpdate_RefreshesAccountsWithoutExchangingOrReplacingTheCredential()
    {
        await using var database = CreateDatabase();
        SeedUsersAndAccounts(database);
        var provider = DataProtectionProvider.Create("PlaidLinkUpdateTests");
        var client = new FakePlaidClient();
        var service = new PlaidConnectionService(database, new SharedBudgetDataService(database), client, provider);

        var first = await service.ExchangePublicTokenAsync(1, "initial-public-token", null, null, CancellationToken.None);
        var tokenBeforeUpdate = (await database.PlaidItems.SingleAsync()).EncryptedAccessToken;
        var second = await service.CompleteUpdateAsync(1, first.PlaidItemId, null, "Updated Sandbox Bank", CancellationToken.None);

        Assert.Equal(first.PlaidItemId, second.PlaidItemId);
        var item = await database.PlaidItems.SingleAsync();
        Assert.Equal("Updated Sandbox Bank", item.InstitutionName);
        Assert.Equal(tokenBeforeUpdate, item.EncryptedAccessToken);
        Assert.Equal(1, client.ExchangeCalls);
    }

    [Fact]
    public async Task UpdateLinkToken_OmitsProductsAndUsesExistingAccessToken()
    {
        var handler = new RecordingHandler();
        var client = new PlaidClient(new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.plaid.com/") },
            Options.Create(new PlaidOptions { Enabled = true, ClientId = "client", ClientSecret = "secret", Products = ["auth"] }));

        await client.CreateLinkTokenAsync(1, updateMode: true, accessToken: "stored-access-token", CancellationToken.None);

        Assert.Contains("\"access_token\":\"stored-access-token\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"products\"", handler.RequestBody, StringComparison.Ordinal);
    }

    private static ClintonFranklandDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ClintonFranklandDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void SeedUsersAndAccounts(ClintonFranklandDbContext database)
    {
        database.Users.AddRange(new User { UserId = 1, UserName = "one", DisplayName = "One" }, new User { UserId = 2, UserName = "two", DisplayName = "Two" });
        database.SharedBudgets.AddRange(new SharedBudget { SharedBudgetId = 1, Name = "One", OwnerUserId = 1 }, new SharedBudget { SharedBudgetId = 2, Name = "Two", OwnerUserId = 2 });
        database.BudgetMembers.AddRange(
            new BudgetMember { SharedBudgetId = 1, UserId = 1, Role = BudgetMemberRole.Owner, Status = BudgetMemberStatus.Active },
            new BudgetMember { SharedBudgetId = 2, UserId = 2, Role = BudgetMemberRole.Owner, Status = BudgetMemberStatus.Active });
        database.Accounts.AddRange(
            new Account { AccountId = 1, AccountName = "Allowed", AccountTypeId = 1, UserId = 1, SharedBudgetId = 1 },
            new Account { AccountId = 2, AccountName = "Denied", AccountTypeId = 1, UserId = 2, SharedBudgetId = 2 });
        database.SaveChanges();
    }

    private sealed class FakePlaidClient : IPlaidClient
    {
        public string AccessToken { get; } = "sandbox-access-token-never-store-in-cleartext";
        public int ExchangeCalls { get; private set; }
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int userId, bool updateMode, string? accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(new PlaidLinkToken("link-token", DateTimeOffset.UtcNow.AddMinutes(30)));
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken)
        {
            ExchangeCalls++;
            return Task.FromResult(new PlaidExchangeResult(AccessToken, "item-1"));
        }
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PlaidDiscoveredAccount>>([new("CaseSensitive_Id", "Checking", "1234", "depository", "checking")]);
        public Task<PlaidSyncPage> SyncTransactionsAsync(string accessToken, string? cursor, CancellationToken cancellationToken) =>
            Task.FromResult(new PlaidSyncPage([], [], [], cursor ?? "cursor", false));
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"link_token\":\"link-token\",\"expiration\":\"2026-07-27T01:00:00Z\"}", Encoding.UTF8, "application/json")
            };
        }
    }
}
