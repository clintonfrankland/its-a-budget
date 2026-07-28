using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        var runId = run.PlaidSyncRunId;
        try
        {
            // Plaid requires a complete loop to use one stable snapshot. A mutation error
            // discards the whole attempt and starts again from the persisted cursor.
            for (var restart = 0; ; restart++)
            {
                await using var transaction = await BeginTransactionAsync(ct);
                try
                {
                    var cursor = item.TransactionsCursor;
                    do
                    {
                        var page = await _client.SyncTransactionsAsync(_protector.Unprotect(item.EncryptedAccessToken), cursor, ct);
                        ApplyPage(item, page, run);
                        cursor = page.NextCursor;
                        if (!page.HasMore) break;
                    } while (true);
                    item.TransactionsCursor = cursor; run.CursorAfter = cursor; run.Status = "completed"; run.CompletedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    if (transaction is not null) await transaction.CommitAsync(ct);
                    break;
                }
                catch (PlaidSyncMutationDuringPaginationException) when (restart < 2)
                {
                    if (transaction is not null) await transaction.RollbackAsync(ct);
                    _db.ChangeTracker.Clear();
                    item = await _db.PlaidItems.SingleAsync(x => x.PlaidItemId == plaidItemId && x.Status == PlaidItemStatus.Active, ct);
                    run = await _db.PlaidSyncRuns.SingleAsync(x => x.PlaidSyncRunId == run.PlaidSyncRunId, ct);
                }
            }
        }
        catch (Exception ex)
        {
            // A provider without transactional support (notably EF's InMemory test
            // provider) can still have the page entities tracked here. Clear them
            // before recording the run failure so a failed loop never leaks staged
            // evidence or advances the cursor as a side effect of failure logging.
            _db.ChangeTracker.Clear();
            run = await _db.PlaidSyncRuns.SingleAsync(x => x.PlaidSyncRunId == runId, CancellationToken.None);
            run.Status = "failed";
            run.ErrorCode = ex.GetType().Name;
            run.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct) =>
        _db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true ? null : await _db.Database.BeginTransactionAsync(ct);

    private void ApplyPage(PlaidItem item, PlaidSyncPage page, PlaidSyncRun run)
    {
        var maps = _db.PlaidAccountMappings.Where(x => x.PlaidItemId == item.PlaidItemId).ToDictionary(x => x.PlaidAccountId, StringComparer.Ordinal);
        foreach (var tx in page.Added.Concat(page.Modified))
        {
            if (!maps.TryGetValue(tx.AccountId, out var map)) continue; // never stage an unapproved account
            var entity = _db.PlaidTransactionStaging.SingleOrDefault(x => x.PlaidItemId == item.PlaidItemId && x.PlaidTransactionId == tx.TransactionId);
            if (entity is null) { entity = new PlaidTransactionStaging { UserId = item.UserId, PlaidItemId = item.PlaidItemId, PlaidTransactionId = tx.TransactionId, FirstSeenAtUtc = DateTime.UtcNow }; _db.PlaidTransactionStaging.Add(entity); }
            entity.BudgetAccountId = map.BudgetAccountId; entity.PlaidAccountId = tx.AccountId; entity.PlaidAmount = tx.Amount; entity.CurrencyCode = tx.IsoCurrencyCode; entity.TransactionDate = tx.Date; entity.IsPending = tx.Pending; entity.PendingTransactionId = tx.PendingTransactionId; entity.MerchantName = tx.MerchantName; entity.MerchantEntityId = tx.MerchantEntityId; entity.Name = tx.Name; entity.IsRemoved = false; entity.LastSeenAtUtc = DateTime.UtcNow;
        }
        foreach (var id in page.Removed) { var entity = _db.PlaidTransactionStaging.SingleOrDefault(x => x.PlaidItemId == item.PlaidItemId && x.PlaidTransactionId == id); if (entity is not null) entity.IsRemoved = true; }
        run.AddedCount += page.Added.Count; run.ModifiedCount += page.Modified.Count; run.RemovedCount += page.Removed.Count;
    }
}
