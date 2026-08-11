using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

/// <summary>
/// The authoritative membership policy for balance-affecting ledger rows.
/// Stored Account.Balance and Account.ClearedBalance are caches written only by
/// explicit opening-balance operations; checkbook, reporting, and projections
/// derive their displayed values from BeginningBalance plus this scope.
/// </summary>
public static class LedgerScope
{
    /// <summary>
    /// Returns readable transactions whose account link and ownership context agree.
    /// Legacy rows with a null/mismatched SharedBudgetId, an owner that differs from
    /// a personal account, or a deleted/missing account are intentionally excluded.
    /// They remain in the database for a reviewed repair or migration; this query
    /// never rewrites historical balances.
    /// </summary>
    public static IQueryable<Transaction> ReadableTransactions(
        this ClintonFranklandDbContext db,
        int userId,
        IReadOnlyCollection<int> readableSharedBudgetIds) =>
        db.Transactions.AsNoTracking().Where(t =>
            t.Account != null &&
            !(t.Account.IsDeleted ?? false) &&
            (t.Account.SharedBudgetId.HasValue
                ? readableSharedBudgetIds.Contains(t.Account.SharedBudgetId.Value) &&
                  t.SharedBudgetId == t.Account.SharedBudgetId
                : !t.SharedBudgetId.HasValue &&
                  t.Account.UserId == userId &&
                  t.UserId == t.Account.UserId));

    /// <summary>Returns the canonical balance ledger for one known account.</summary>
    public static IQueryable<Transaction> TransactionsForAccount(
        this ClintonFranklandDbContext db,
        Account account) =>
        db.Transactions.AsNoTracking().Where(t =>
            t.AccountId == account.AccountId &&
            (account.SharedBudgetId.HasValue
                ? t.SharedBudgetId == account.SharedBudgetId
                : !t.SharedBudgetId.HasValue && t.UserId == account.UserId));

    /// <summary>In-memory counterpart used by repair previews and regression fixtures.</summary>
    public static bool BelongsToAccount(Transaction transaction, Account account) =>
        transaction.AccountId == account.AccountId &&
        !(account.IsDeleted ?? false) &&
        (account.SharedBudgetId.HasValue
            ? transaction.SharedBudgetId == account.SharedBudgetId
            : !transaction.SharedBudgetId.HasValue && transaction.UserId == account.UserId);
}
