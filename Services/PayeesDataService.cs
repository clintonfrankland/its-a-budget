using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClintonFrankland.Services;

public class PayeesDataService
{
    private readonly ClintonFranklandDbContext _db;
    private sealed record WindowTransaction(int PayeeId, DateOnly TransactionDate, decimal Amount);

    public PayeesDataService(ClintonFranklandDbContext db) => _db = db;

    public async Task<List<PayeeSummaryViewModel>> GetPayeeSummariesAsync(int userId, bool includeDeleted, DateOnly today)
    {
        var oneYearStart = today.AddYears(-1);
        var sixMonthStart = today.AddMonths(-6);
        var thirtyDayStart = today.AddDays(-30);

        var payees = await _db.Payees
            .AsNoTracking()
            .Where(p => p.UserId == userId && (includeDeleted || !p.IsDeleted))
            .OrderBy(p => p.PayeeName)
            .ToListAsync();

        var payeeIds = payees.Select(p => p.PayeeId).ToList();
        var lastTransactions = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && payeeIds.Contains(t.PayeeId))
            .GroupBy(t => t.PayeeId)
            .Select(g => new { PayeeId = g.Key, LastTransactionDate = g.Max(t => (DateOnly?)t.TransactionDate) })
            .ToDictionaryAsync(x => x.PayeeId, x => x.LastTransactionDate);

        var budgetCounts = await _db.Budgets
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.PayeeId.HasValue && payeeIds.Contains(b.PayeeId.Value))
            .GroupBy(b => b.PayeeId!.Value)
            .Select(g => new { PayeeId = g.Key, BudgetCount = g.Count() })
            .ToDictionaryAsync(x => x.PayeeId, x => x.BudgetCount);

        var windowTransactions = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && payeeIds.Contains(t.PayeeId) && t.TransactionDate >= oneYearStart)
            .Select(t => new WindowTransaction(t.PayeeId, t.TransactionDate, t.Amount))
            .ToListAsync();

        var transactionsByPayee = windowTransactions
            .GroupBy(t => t.PayeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var summaries = payees.Select(p => new PayeeSummaryViewModel
        {
            PayeeId = p.PayeeId,
            PayeeName = p.PayeeName,
            IsDeleted = p.IsDeleted,
            LastTransactionDate = lastTransactions.GetValueOrDefault(p.PayeeId),
            BudgetCount = budgetCounts.GetValueOrDefault(p.PayeeId),
            Income30Days = SumIncome(transactionsByPayee.GetValueOrDefault(p.PayeeId) ?? [], thirtyDayStart),
            Expense30Days = SumExpense(transactionsByPayee.GetValueOrDefault(p.PayeeId) ?? [], thirtyDayStart),
            Income6Months = SumIncome(transactionsByPayee.GetValueOrDefault(p.PayeeId) ?? [], sixMonthStart),
            Expense6Months = SumExpense(transactionsByPayee.GetValueOrDefault(p.PayeeId) ?? [], sixMonthStart),
            Income1Year = SumIncome(transactionsByPayee.GetValueOrDefault(p.PayeeId) ?? [], oneYearStart),
            Expense1Year = SumExpense(transactionsByPayee.GetValueOrDefault(p.PayeeId) ?? [], oneYearStart)
        }).ToList();

        ApplyDuplicateHints(summaries);
        return summaries;
    }

    public async Task<Payee?> GetPayeeForEditAsync(int userId, int payeeId) =>
        await _db.Payees.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.PayeeId == payeeId);

    public async Task UpdatePayeeAsync(int userId, int payeeId, string payeeName)
    {
        var normalizedName = NormalizePayeeName(payeeName);
        var payee = await _db.Payees.FirstOrDefaultAsync(p => p.UserId == userId && p.PayeeId == payeeId)
            ?? throw new InvalidOperationException("Payee was not found.");

        if (payee.IsDeleted)
            throw new InvalidOperationException("Deleted payees cannot be edited.");

        var duplicateExists = await _db.Payees.AnyAsync(p =>
            p.UserId == userId &&
            p.PayeeId != payeeId &&
            !p.IsDeleted &&
            p.PayeeName.ToLower() == normalizedName.ToLower());

        if (duplicateExists)
            throw new InvalidOperationException("Another active payee already uses that name.");

        payee.PayeeName = normalizedName;
        await _db.SaveChangesAsync();
    }

    public async Task<PayeeMergePreview> PreviewMergeAsync(int userId, int keepPayeeId, int removePayeeId)
    {
        var (keep, remove) = await ValidateMergePayeesAsync(userId, keepPayeeId, removePayeeId);
        var transactionCount = await _db.Transactions.CountAsync(t => t.UserId == userId && t.PayeeId == removePayeeId);
        var budgetCount = await _db.Budgets.CountAsync(b => b.UserId == userId && b.PayeeId == removePayeeId);

        return new PayeeMergePreview(keep.PayeeId, keep.PayeeName, remove.PayeeId, remove.PayeeName, transactionCount, budgetCount);
    }

    public async Task<PayeeMergeResult> MergePayeesAsync(int userId, int keepPayeeId, int removePayeeId, string confirmationName)
    {
        var preview = await PreviewMergeAsync(userId, keepPayeeId, removePayeeId);
        if (!string.Equals(confirmationName?.Trim(), preview.KeepPayeeName, StringComparison.Ordinal))
            throw new InvalidOperationException("Confirmation must match the destination payee name.");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        var (keep, remove) = await ValidateMergePayeesAsync(userId, keepPayeeId, removePayeeId);

        var transactions = await _db.Transactions.Where(t => t.UserId == userId && t.PayeeId == remove.PayeeId).ToListAsync();
        foreach (var item in transactions)
            item.PayeeId = keep.PayeeId;

        var budgets = await _db.Budgets.Where(b => b.UserId == userId && b.PayeeId == remove.PayeeId).ToListAsync();
        foreach (var item in budgets)
            item.PayeeId = keep.PayeeId;

        remove.IsDeleted = true;
        await _db.SaveChangesAsync();

        if (transaction is not null)
            await transaction.CommitAsync();

        return new PayeeMergeResult(keep.PayeeId, remove.PayeeId, transactions.Count, budgets.Count);
    }

    private async Task<(Payee Keep, Payee Remove)> ValidateMergePayeesAsync(int userId, int keepPayeeId, int removePayeeId)
    {
        if (keepPayeeId == removePayeeId)
            throw new InvalidOperationException("Choose two different payees to merge.");

        var keep = await _db.Payees.FirstOrDefaultAsync(p => p.UserId == userId && p.PayeeId == keepPayeeId)
            ?? throw new InvalidOperationException("Destination payee was not found.");
        var remove = await _db.Payees.FirstOrDefaultAsync(p => p.UserId == userId && p.PayeeId == removePayeeId)
            ?? throw new InvalidOperationException("Source payee was not found.");

        if (keep.IsDeleted || remove.IsDeleted)
            throw new InvalidOperationException("Deleted payees cannot be merged.");

        return (keep, remove);
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
    {
        if (!_db.Database.IsRelational())
            return null;

        return await _db.Database.BeginTransactionAsync();
    }

    private static string NormalizePayeeName(string payeeName)
    {
        var normalized = string.Join(' ', (payeeName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Payee name is required.");
        if (normalized.Length > 64)
            throw new InvalidOperationException("Payee name must be 64 characters or fewer.");

        return normalized;
    }

    private static decimal SumIncome(IEnumerable<WindowTransaction> transactions, DateOnly startDate) =>
        transactions.Where(t => t.TransactionDate >= startDate && t.Amount > 0).Sum(t => t.Amount);

    private static decimal SumExpense(IEnumerable<WindowTransaction> transactions, DateOnly startDate) =>
        transactions.Where(t => t.TransactionDate >= startDate && t.Amount < 0).Sum(t => Math.Abs(t.Amount));

    private static void ApplyDuplicateHints(List<PayeeSummaryViewModel> summaries)
    {
        var lookup = summaries
            .Where(p => !p.IsDeleted)
            .GroupBy(p => BuildDuplicateKey(p.PayeeName))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(p => p.PayeeName).OrderBy(n => n)));

        foreach (var summary in summaries)
        {
            var key = BuildDuplicateKey(summary.PayeeName);
            if (lookup.TryGetValue(key, out var names))
                summary.DuplicateHint = names;
        }
    }

    private static string BuildDuplicateKey(string payeeName)
    {
        var normalized = new string(payeeName
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        foreach (var suffix in new[] { "incorporated", "corporation", "company", "limited", "llc", "ltd", "inc", "co" })
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
                normalized = normalized[..^suffix.Length];
        }

        return normalized;
    }
}
