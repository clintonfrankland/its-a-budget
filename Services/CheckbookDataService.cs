using System.Data;
using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class CheckbookDataService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public CheckbookDataService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public async Task<Account?> GetAccountForUserAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Accounts
            .AsNoTracking()
            .Where(a => !(a.IsDeleted ?? false) && (a.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(a.SharedBudgetId.Value)
                : a.UserId == userId))
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.AccountId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Transaction>> GetTransactionsForUserAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.ReadableTransactions(userId, sharedBudgetIds).Include(t => t.Payee).Include(t => t.Category)
            .OrderByDescending(t => t.Cleared).ThenBy(t => t.TransactionDate).ThenByDescending(t => t.Amount)
            .ToListAsync();
    }

    public Task<List<Payee>> GetPayeesForUserAsync(int userId) => _db.Payees.AsNoTracking().Where(p => p.UserId == userId && !p.IsDeleted).OrderBy(p => p.PayeeName).ToListAsync();
    public async Task<List<Category>> GetCategoriesForUserAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(c.SharedBudgetId.Value)
                : c.UserId == userId)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<decimal> GetTransactionSumAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.ReadableTransactions(userId, sharedBudgetIds)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
    }

    public async Task<decimal> GetBeginningBalanceAsync(int userId) =>
        CurrencyPolicy.Round((await GetAccountForUserAsync(userId))?.BeginningBalance ?? 0m);

    public async Task<decimal> GetCurrentBalanceAsync(int userId) =>
        CurrencyPolicy.Round(await GetBeginningBalanceAsync(userId) + await GetTransactionSumAsync(userId));

    public async Task<decimal> GetClearedBalanceAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var clearedAmount = await _db.ReadableTransactions(userId, sharedBudgetIds)
            .Where(t => t.Cleared)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return CurrencyPolicy.Round(await GetBeginningBalanceAsync(userId) + clearedAmount);
    }

    public async Task<List<Budget>> GetBudgetsForUserAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Frequency)
            .Include(b => b.Payee)
            .Where(b => b.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(b.SharedBudgetId.Value)
                : b.UserId == userId)
            .ToListAsync();
    }

    /// <summary>
    /// Returns the raw transaction sum per category for each of the last <paramref name="months"/> complete months
    /// (current month excluded). Keyed by category name; array is oldest-first.
    /// </summary>
    public async Task<Dictionary<string, decimal[]>> GetMonthlyCategoryTotalsAsync(int userId, int months)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-months);

        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var transactions = await _db.ReadableTransactions(userId, sharedBudgetIds)
            .Include(t => t.Category)
            .Where(t =>
                t.TransactionDate >= firstMonth &&
                t.TransactionDate < new DateOnly(today.Year, today.Month, 1))
            .ToListAsync();

        var monthStarts = Enumerable.Range(0, months)
            .Select(i => firstMonth.AddMonths(i))
            .ToList();

        var result = new Dictionary<string, decimal[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in transactions.GroupBy(t => t.Category?.CategoryName ?? "Uncategorized"))
        {
            result[g.Key] = monthStarts
                .Select(m => g.Where(t => t.TransactionDate.Year == m.Year && t.TransactionDate.Month == m.Month)
                              .Sum(t => t.Amount))
                .ToArray();
        }
        return result;
    }

    public async Task<List<(string Category, decimal Total)>> GetMonthlyExpensesByCategoryAsync(int userId, int year, int month)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var transactions = await _db.ReadableTransactions(userId, sharedBudgetIds)
            .Include(t => t.Category)
            .Where(t =>
                t.TransactionDate >= startDate &&
                t.TransactionDate <= endDate &&
                t.Amount < 0)
            .ToListAsync();

        return transactions
            .GroupBy(t => t.Category?.CategoryName ?? "Uncategorized")
            .Select(g => (g.Key, g.Sum(t => -t.Amount)))
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int userId, int id)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.ReadableTransactions(userId, sharedBudgetIds)
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.TransactionId == id);
    }

    public async Task SaveTransactionAsync(
        int userId,
        int transactionId,
        DateOnly transactionDate,
        string payeeName,
        string categoryName,
        decimal amount,
        bool cleared,
        string? notes,
        string? attachmentPath)
    {
        if (transactionDate == DateOnly.MinValue)
            throw new InvalidOperationException("Transaction date is required.");

        var isNew = transactionId == -1;
        var txn = isNew
            ? new Transaction { UserId = userId, SharedBudgetId = await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId) }
            : await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        if (txn is null)
            return;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, txn.SharedBudgetId, txn.UserId))
            return;

        var roundedAmount = CurrencyPolicy.RoundSignedSqlAmount(amount, CurrencyPolicy.TransactionPrecision);
        var categoryId = await GetOrCreateCategoryAsync(categoryName, userId, txn.SharedBudgetId);
        var payeeId = await GetOrCreatePayeeAsync(payeeName, userId);

        if (categoryId <= 0) categoryId = await GetOrCreateCategoryAsync("Uncategorized", userId, txn.SharedBudgetId);
        if (payeeId <= 0) payeeId = await GetOrCreatePayeeAsync("Unknown", userId);

        var account = txn.SharedBudgetId.HasValue
            ? await _db.Accounts
                .AsNoTracking()
                .Where(a => a.SharedBudgetId == txn.SharedBudgetId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.AccountId)
                .FirstOrDefaultAsync()
            : await GetAccountForUserAsync(userId);
        var accountId = account?.AccountId ?? 1;

        txn.TransactionDate = transactionDate;
        txn.PayeeId = payeeId;
        txn.CategoryId = categoryId;
        txn.AccountId = accountId;
        txn.Amount = roundedAmount;
        txn.Cleared = cleared;
        txn.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        txn.AttachmentPath = attachmentPath;

        if (isNew) _db.Transactions.Add(txn);
        await _db.SaveChangesAsync();
    }

    /// <summary>Resolves the writable account and scope used by both import preview and persistence.</summary>
    public async Task<ImportDestination?> GetImportDestinationAsync(int userId)
    {
        var user = userId > 0
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.UserId == userId && !user.IsDeleted)
            : null;
        if (user is null)
            return null;

        // Read the selection from storage, not a possibly stale tracked User from the preview.
        var memberships = _db.BudgetMembers.AsNoTracking()
            .Where(member => member.UserId == userId && member.Status == BudgetMemberStatus.Active);
        var activeSharedBudgetId = user.ActiveSharedBudgetId.HasValue &&
            await memberships.AnyAsync(member => member.SharedBudgetId == user.ActiveSharedBudgetId.Value)
            ? user.ActiveSharedBudgetId
            : await memberships.OrderBy(member => member.SharedBudgetId)
                .Select(member => (int?)member.SharedBudgetId).FirstOrDefaultAsync();
        if (!activeSharedBudgetId.HasValue && !await _db.Accounts.AsNoTracking().AnyAsync(account =>
                account.UserId == userId && account.SharedBudgetId == null && !(account.IsDeleted ?? false)))
            return null;

        activeSharedBudgetId ??= await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId);
        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, activeSharedBudgetId, userId))
            return null;

        // Prefer the active budget, retaining only the user's own unscoped legacy accounts as a fallback.
        var account = await _db.Accounts.AsNoTracking()
            .Where(account => !(account.IsDeleted ?? false) &&
                (activeSharedBudgetId.HasValue && account.SharedBudgetId == activeSharedBudgetId ||
                 account.SharedBudgetId == null && account.UserId == userId))
            .OrderByDescending(account => account.SharedBudgetId.HasValue)
            .ThenByDescending(account => account.IsDefault)
            .ThenBy(account => account.AccountId)
            .FirstOrDefaultAsync();
        if (account is null || !await _sharedBudgets.CanManageFinancialDataAsync(userId, account.SharedBudgetId, account.UserId))
            return null;

        return new ImportDestination(account.AccountId, account.SharedBudgetId, account.AccountName, activeSharedBudgetId);
    }

    public async Task ImportTransactionsAsync(
        int userId,
        IReadOnlyCollection<ImportedTransaction> transactions,
        ImportDestination? expectedDestination = null)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        // Validate the entire file before tracking entities so a bad later row cannot leave prior rows pending.
        foreach (var transaction in transactions)
        {
            if (transaction.TransactionDate == DateOnly.MinValue)
                throw new InvalidOperationException("Transaction date is required.");

            _ = CurrencyPolicy.RoundSignedSqlAmount(transaction.Amount, CurrencyPolicy.TransactionPrecision);
        }

        if (transactions.Count == 0)
            return;

        // Hold the destination, active-budget selection, and membership reads stable until commit.
        // InMemory has no relational isolation API; its existing test transaction behavior is retained.
        await using var databaseTransaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : await _db.Database.BeginTransactionAsync();
        try
        {
            var destination = await GetImportDestinationAsync(userId)
                ?? throw new InvalidOperationException("No writable account is available for this import.");
            if (expectedDestination is not null &&
                (destination.AccountId != expectedDestination.AccountId ||
                 destination.SharedBudgetId != expectedDestination.SharedBudgetId ||
                 destination.ActiveSharedBudgetId != expectedDestination.ActiveSharedBudgetId))
                throw new InvalidOperationException("The import account or active budget changed. Preview the file again before importing.");

            var sharedBudgetId = destination.SharedBudgetId;
            var accountId = destination.AccountId;

            var payeeNames = transactions.Select(t => NormalizeName(t.PayeeName, "Unknown")).Distinct().ToList();
            var categoryNames = transactions.Select(t => NormalizeName(t.CategoryName, "Uncategorized")).Distinct().ToList();
            var payees = (await _db.Payees.Where(p => p.UserId == userId && payeeNames.Contains(p.PayeeName)).ToListAsync())
                .ToDictionary(p => p.PayeeName, StringComparer.Ordinal);
            var categories = (await _db.Categories.Where(c => c.SharedBudgetId == sharedBudgetId && (sharedBudgetId.HasValue || c.UserId == userId) && categoryNames.Contains(c.CategoryName!)).ToListAsync())
                .ToDictionary(c => c.CategoryName!, StringComparer.Ordinal);

            foreach (var import in transactions)
            {
                var payeeName = NormalizeName(import.PayeeName, "Unknown");
                if (!payees.TryGetValue(payeeName, out var payee))
                {
                    payee = new Payee { PayeeName = payeeName, UserId = userId, IsDeleted = false };
                    payees.Add(payeeName, payee);
                    _db.Payees.Add(payee);
                }

                var categoryName = NormalizeName(import.CategoryName, "Uncategorized");
                if (!categories.TryGetValue(categoryName, out var category))
                {
                    category = new Category { CategoryName = categoryName, UserId = userId, SharedBudgetId = sharedBudgetId };
                    categories.Add(categoryName, category);
                    _db.Categories.Add(category);
                }

                _db.Transactions.Add(new Transaction
                {
                    UserId = userId,
                    SharedBudgetId = sharedBudgetId,
                    TransactionDate = import.TransactionDate,
                    Payee = payee,
                    Category = category,
                    AccountId = accountId,
                    Amount = CurrencyPolicy.RoundSignedSqlAmount(import.Amount, CurrencyPolicy.TransactionPrecision),
                    Cleared = import.Cleared,
                    Notes = string.IsNullOrWhiteSpace(import.Notes) ? null : import.Notes.Trim(),
                    AttachmentPath = import.AttachmentPath
                });
            }

            await _db.SaveChangesAsync();
            await databaseTransaction.CommitAsync();
        }
        catch
        {
            await databaseTransaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task DeleteTransactionAsync(int userId, int id)
    {
        var txn = await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id);
        if (txn is null) return;
        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, txn.SharedBudgetId, txn.UserId))
            return;

        _db.Transactions.Remove(txn);
        await _db.SaveChangesAsync();
    }

    public async Task SetTransactionClearedAsync(int userId, int transactionId, bool cleared)
    {
        var transaction = await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
        if (transaction is null)
            return;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, transaction.SharedBudgetId, transaction.UserId))
            return;

        transaction.Cleared = cleared;
        await _db.SaveChangesAsync();
    }

    public Task<int> GetOrCreateCategoryAsync(string categoryName, int userId) =>
        GetOrCreateCategoryAsync(categoryName, userId, null);

    private async Task<int> GetOrCreateCategoryAsync(string categoryName, int userId, int? sharedBudgetId)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return -1;
        var category = sharedBudgetId.HasValue
            ? await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.SharedBudgetId == sharedBudgetId)
            : await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.UserId == userId);
        if (category != null) return category.CategoryId;
        var newCategory = new Category
        {
            CategoryName = categoryName,
            UserId = userId,
            SharedBudgetId = sharedBudgetId ?? await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId)
        };
        _db.Categories.Add(newCategory);
        await _db.SaveChangesAsync();
        return newCategory.CategoryId;
    }

    public async Task<int> GetOrCreatePayeeAsync(string payeeName, int userId)
    {
        if (string.IsNullOrWhiteSpace(payeeName)) return -1;
        var payee = await _db.Payees.FirstOrDefaultAsync(p => p.PayeeName == payeeName && p.UserId == userId);
        if (payee != null) return payee.PayeeId;
        var newPayee = new Payee { PayeeName = payeeName, UserId = userId, IsDeleted = false };
        _db.Payees.Add(newPayee);
        await _db.SaveChangesAsync();
        return newPayee.PayeeId;
    }

    private static string NormalizeName(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed record ImportedTransaction(
    DateOnly TransactionDate,
    string? PayeeName,
    string? CategoryName,
    decimal Amount,
    bool Cleared = false,
    string? Notes = null,
    string? AttachmentPath = null);

public sealed record ImportDestination(
    int AccountId,
    int? SharedBudgetId,
    string AccountName,
    int? ActiveSharedBudgetId = null);
