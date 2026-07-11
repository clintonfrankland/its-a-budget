using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class InsightsDataService
{
    public const string UncategorizedCategoryName = "Uncategorized";
    public const string UnknownPayeeName = "Unknown payee";
    private const int TrendCompleteMonths = 6;

    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public InsightsDataService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public async Task<List<CategorySpendingTotal>> GetMonthlyCategoryTotalsAsync(int userId, DateOnly selectedMonth)
    {
        var monthStart = FirstOfMonth(selectedMonth);
        var monthEnd = monthStart.AddMonths(1);
        var transactions = await GetExpenseTransactionsAsync(userId, monthStart, monthEnd);

        return transactions
            .GroupBy(t => GetCategoryName(t, userId), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategorySpendingTotal(g.Key, g.Sum(t => -t.Amount)))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.CategoryName)
            .ToList();
    }

    public async Task<List<CategoryTrendRow>> GetCategoryTrendAsync(int userId, DateOnly selectedMonth)
    {
        var months = GetTrendMonths(selectedMonth);
        var firstMonth = months[0].StartDate;
        var lastExclusive = months[^1].StartDate.AddMonths(1);
        var transactions = await GetExpenseTransactionsAsync(userId, firstMonth, lastExclusive);

        return transactions
            .GroupBy(t => GetCategoryName(t, userId), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategoryTrendRow(
                g.Key,
                months
                    .Select(m => g
                        .Where(t => IsSameMonth(t.TransactionDate, m.StartDate))
                        .Sum(t => -t.Amount))
                    .ToList()))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.CategoryName)
            .ToList();
    }

    public async Task<List<TopPayeeSpending>> GetTopPayeesAsync(int userId, DateOnly selectedMonth, int limit = 5)
    {
        var monthStart = FirstOfMonth(selectedMonth);
        var monthEnd = monthStart.AddMonths(1);
        var transactions = await GetExpenseTransactionsAsync(userId, monthStart, monthEnd);

        return transactions
            .GroupBy(t => GetPayeeName(t, userId), StringComparer.OrdinalIgnoreCase)
            .Select(g => new TopPayeeSpending(g.Key, g.Sum(t => -t.Amount), g.Count()))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.PayeeName)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    public static List<InsightsMonth> GetTrendMonths(DateOnly selectedMonth)
    {
        var monthStart = FirstOfMonth(selectedMonth);
        return Enumerable.Range(0, TrendCompleteMonths + 1)
            .Select(i => monthStart.AddMonths(i - TrendCompleteMonths))
            .Select(m => new InsightsMonth(m, m.ToString("MMM yyyy")))
            .ToList();
    }

    private async Task<List<Transaction>> GetExpenseTransactionsAsync(int userId, DateOnly startInclusive, DateOnly endExclusive)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Transactions
            .AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Payee)
            .Where(t =>
                (t.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(t.SharedBudgetId.Value)
                    : t.UserId == userId) &&
                t.Account != null &&
                (t.Account.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(t.Account.SharedBudgetId.Value)
                    : t.Account.UserId == userId) &&
                t.Amount < 0 &&
                t.TransactionDate >= startInclusive &&
                t.TransactionDate < endExclusive)
            .ToListAsync();
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static bool IsSameMonth(DateOnly transactionDate, DateOnly monthStart) =>
        transactionDate.Year == monthStart.Year && transactionDate.Month == monthStart.Month;

    private static string GetCategoryName(Transaction transaction, int userId)
    {
        if (transaction.Category is null)
            return UncategorizedCategoryName;

        if (transaction.SharedBudgetId.HasValue
            ? transaction.Category.SharedBudgetId != transaction.SharedBudgetId
            : transaction.Category.SharedBudgetId.HasValue || transaction.Category.UserId != userId)
            return UncategorizedCategoryName;

        return string.IsNullOrWhiteSpace(transaction.Category.CategoryName)
            ? UncategorizedCategoryName
            : transaction.Category.CategoryName.Trim();
    }

    private static string GetPayeeName(Transaction transaction, int userId)
    {
        if (transaction.Payee is null)
            return UnknownPayeeName;

        if (!transaction.SharedBudgetId.HasValue && transaction.Payee.UserId != userId)
            return UnknownPayeeName;

        return string.IsNullOrWhiteSpace(transaction.Payee.PayeeName)
            ? UnknownPayeeName
            : transaction.Payee.PayeeName.Trim();
    }
}
