using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

/// <summary>Consumes durable webhook receipts in a fresh scope; requests never run DbContext work in Task.Run.</summary>
public sealed class PlaidWebhookSyncWorker(IServiceScopeFactory scopes, ILogger<PlaidWebhookSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DrainAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Plaid webhook queue iteration failed."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    internal async Task DrainAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<PlaidTransactionSyncService>();
        var staleBefore = DateTime.UtcNow.AddMinutes(-15);
        var receipt = await db.PlaidWebhookDeliveries.OrderBy(x => x.ReceivedAtUtc)
            .FirstOrDefaultAsync(x => x.Status == "queued" ||
                (x.Status == "processing" && x.ProcessingStartedAtUtc < staleBefore), ct);
        if (receipt is null) return;
        // Processing is a lease, rather than a terminal state: a crash after claim
        // is retried once the lease becomes stale.
        receipt.Status = "processing";
        receipt.ProcessingStartedAtUtc = DateTime.UtcNow;
        receipt.AttemptCount++;
        await db.SaveChangesAsync(ct);
        try
        {
            var item = await db.PlaidItems.SingleOrDefaultAsync(x => x.ItemId == receipt.ItemId && x.Status == PlaidItemStatus.Active, ct);
            if (item is not null) await sync.SyncItemAsync(item.PlaidItemId, ct);
            receipt.Status = "completed"; receipt.CompletedAtUtc = DateTime.UtcNow; receipt.ProcessingStartedAtUtc = null; receipt.ErrorCode = null;
        }
        catch (Exception ex)
        {
            receipt.Status = "queued"; receipt.ProcessingStartedAtUtc = null; receipt.ErrorCode = ex.GetType().Name;
        }
        await db.SaveChangesAsync(ct);
    }
}
