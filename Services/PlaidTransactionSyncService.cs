using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

/// <summary>Imports Plaid evidence into staging only; it never writes cfTransactions.</summary>
public sealed class PlaidTransactionSyncService
{
    private readonly ClintonFranklandDbContext _db; private readonly IPlaidClient _client; private readonly IDataProtector _protector;
    public PlaidTransactionSyncService(ClintonFranklandDbContext db, IPlaidClient client, IDataProtectionProvider protection)
    { _db = db; _client = client; _protector = protection.CreateProtector("BudgetApp.Plaid.AccessToken.v1"); }

    public async Task SyncItemAsync(int plaidItemId, CancellationToken ct)
    {
        var item = await _db.PlaidItems.SingleAsync(x => x.PlaidItemId == plaidItemId && x.Status == PlaidItemStatus.Active, ct);
        var run = new PlaidSyncRun { PlaidItemId = item.PlaidItemId, CursorBefore = item.TransactionsCursor, StartedAtUtc = DateTime.UtcNow };
        _db.PlaidSyncRuns.Add(run); await _db.SaveChangesAsync(ct);
        try
        {
            // Each successful page stages evidence, but the durable item cursor advances only after the full stable loop.
            var cursor = item.TransactionsCursor; string? next; do
            {
                var page = await _client.SyncTransactionsAsync(_protector.Unprotect(item.EncryptedAccessToken), cursor, ct);
                await ApplyPageAsync(item, page, run, ct); next = page.NextCursor; cursor = next;
                if (!page.HasMore) break;
            } while (true);
            item.TransactionsCursor = cursor; run.CursorAfter = cursor; run.Status = "completed"; run.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        { run.Status = "failed"; run.ErrorCode = ex.GetType().Name; run.CompletedAtUtc = DateTime.UtcNow; await _db.SaveChangesAsync(CancellationToken.None); throw; }
    }

    private async Task ApplyPageAsync(PlaidItem item, PlaidSyncPage page, PlaidSyncRun run, CancellationToken ct)
    {
        var maps = await _db.PlaidAccountMappings.Where(x => x.PlaidItemId == item.PlaidItemId).ToDictionaryAsync(x => x.PlaidAccountId, StringComparer.Ordinal, ct);
        foreach (var tx in page.Added.Concat(page.Modified))
        {
            if (!maps.TryGetValue(tx.AccountId, out var map)) continue; // never stage an unapproved account
            var entity = await _db.PlaidTransactionStaging.SingleOrDefaultAsync(x => x.PlaidItemId == item.PlaidItemId && x.PlaidTransactionId == tx.TransactionId, ct);
            if (entity is null) { entity = new PlaidTransactionStaging { UserId = item.UserId, PlaidItemId = item.PlaidItemId, PlaidTransactionId = tx.TransactionId, FirstSeenAtUtc = DateTime.UtcNow }; _db.PlaidTransactionStaging.Add(entity); }
            entity.BudgetAccountId = map.BudgetAccountId; entity.PlaidAccountId = tx.AccountId; entity.PlaidAmount = tx.Amount; entity.CurrencyCode = tx.IsoCurrencyCode; entity.TransactionDate = tx.Date; entity.IsPending = tx.Pending; entity.PendingTransactionId = tx.PendingTransactionId; entity.MerchantName = tx.MerchantName; entity.Name = tx.Name; entity.IsRemoved = false; entity.LastSeenAtUtc = DateTime.UtcNow;
        }
        foreach (var id in page.Removed) { var entity = await _db.PlaidTransactionStaging.SingleOrDefaultAsync(x => x.PlaidItemId == item.PlaidItemId && x.PlaidTransactionId == id, ct); if (entity is not null) entity.IsRemoved = true; }
        run.AddedCount += page.Added.Count; run.ModifiedCount += page.Modified.Count; run.RemovedCount += page.Removed.Count; await _db.SaveChangesAsync(ct);
    }
}
