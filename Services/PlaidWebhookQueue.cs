using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ClintonFrankland.Services;

/// <summary>Durably records an authenticated Plaid webhook. Duplicate delivery retries are harmless.</summary>
public sealed class PlaidWebhookQueue(ClintonFranklandDbContext database)
{
    public async Task<bool> EnqueueAsync(PlaidWebhookVerification verification, string itemId, string? webhookType, CancellationToken cancellationToken)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{verification.KeyId}:{verification.IssuedAtUnixSeconds}:{verification.RequestBodySha256}"))).ToLowerInvariant();
        if (await database.PlaidWebhookDeliveries.AnyAsync(x => x.DeliveryKey == key, cancellationToken)) return false;

        database.PlaidWebhookDeliveries.Add(new PlaidWebhookDelivery
        {
            DeliveryKey = key, ItemId = itemId, WebhookType = webhookType,
            ReceivedAtUtc = DateTime.UtcNow, QueuedAtUtc = DateTime.UtcNow
        });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // A concurrent retry inserted the unique authenticated-delivery key first.
            return false;
        }
    }
}
