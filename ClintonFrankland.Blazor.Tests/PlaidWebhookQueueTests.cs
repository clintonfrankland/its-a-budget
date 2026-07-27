using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidWebhookQueueTests
{
    [Fact]
    public async Task StaleProcessingLease_IsRecoveredAndCompleted()
    {
        var services = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDataProtection();
        services.AddDbContext<ClintonFranklandDbContext>(options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddScoped<PlaidTransactionSyncService>();
        services.AddScoped<IPlaidClient, NoopPlaidClient>();
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
            db.PlaidWebhookDeliveries.Add(new PlaidWebhookDelivery
            {
                DeliveryKey = "delivery-1", ItemId = "missing-item", Status = "processing", AttemptCount = 1,
                ProcessingStartedAtUtc = DateTime.UtcNow.AddMinutes(-16), ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-20)
            });
            await db.SaveChangesAsync();
        }

        var worker = new PlaidWebhookSyncWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PlaidWebhookSyncWorker>.Instance);
        await worker.DrainAsync(CancellationToken.None);

        await using var verificationScope = provider.CreateAsyncScope();
        var delivery = await verificationScope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>().PlaidWebhookDeliveries.SingleAsync();
        Assert.Equal("completed", delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Null(delivery.ProcessingStartedAtUtc);
    }

    private sealed class NoopPlaidClient : IPlaidClient
    {
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int u, bool a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string p, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task RemoveItemAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidSyncPage> SyncTransactionsAsync(string a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken c) => throw new NotSupportedException();
    }
}
