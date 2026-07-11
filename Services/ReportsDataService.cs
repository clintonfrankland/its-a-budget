using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class ReportsDataService
{
    private static readonly DateTime NoEndDate = new(1970, 1, 1);
    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public ReportsDataService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public async Task<List<SpendPlanRow>> GetSpendVsPlanAsync(int userId, DateOnly selectedMonth)
    {
        var start = FirstOfMonth(selectedMonth);
        var end = start.AddMonths(1);
        var readable = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);

        var targets = await _db.CategoryBudgetTargets.AsNoTracking().Include(t => t.Category)
            .Where(t => t.BudgetMonth == start && (t.SharedBudgetId.HasValue
                ? readable.Contains(t.SharedBudgetId.Value)
                : t.UserId == userId))
            .ToListAsync();
        var transactions = await ReadableTransactions(userId, readable)
            .Include(t => t.Category)
            .Where(t => t.Amount < 0 && t.TransactionDate >= start && t.TransactionDate < end)
            .ToListAsync();

        var planned = targets.GroupBy(t => CategoryName(t.Category, userId, t.SharedBudgetId, readable), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => CurrencyPolicy.Round(g.Sum(t => t.PlannedAmount)), StringComparer.OrdinalIgnoreCase);
        var actual = transactions.GroupBy(t => CategoryName(t.Category, userId, t.SharedBudgetId, readable), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => CurrencyPolicy.Round(g.Sum(t => -t.Amount)), StringComparer.OrdinalIgnoreCase);

        return planned.Keys.Union(actual.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(name => new SpendPlanRow(name, planned.GetValueOrDefault(name), actual.GetValueOrDefault(name)))
            .OrderBy(r => r.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<List<CategoryTrendRow>> GetCategoryTrendsAsync(int userId, DateOnly selectedMonth) =>
        new InsightsDataService(_db, _sharedBudgets).GetCategoryTrendAsync(userId, selectedMonth);

    public async Task<CashflowReport> GetCashflowAsync(int userId, int horizonDays, DateOnly? asOf = null)
    {
        if (horizonDays < 30)
            throw new ArgumentOutOfRangeException(nameof(horizonDays), "Forecast horizon must be at least 30 days.");

        var today = asOf ?? DateOnly.FromDateTime(DateTime.Today);
        var end = today.AddDays(horizonDays);
        var readable = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var accounts = await ReadableAccounts(userId, readable).ToListAsync();
        var accountIds = accounts.Select(a => a.AccountId).ToList();
        var posted = await ReadableTransactions(userId, readable)
            .Where(t => accountIds.Contains(t.AccountId) && t.TransactionDate <= today)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var starting = CurrencyPolicy.Round(accounts.Sum(a => a.BeginningBalance) + posted);
        var budgets = await _db.Budgets.AsNoTracking()
            .Where(b => b.SharedBudgetId.HasValue ? readable.Contains(b.SharedBudgetId.Value) : b.UserId == userId)
            .ToListAsync();

        var occurrences = new List<(DateOnly Date, int Id, string Name, decimal Amount)>();
        foreach (var budget in budgets)
        {
            var due = DateOnly.FromDateTime((budget.NextDueDate ?? today.ToDateTime(TimeOnly.MinValue)).Date);
            var frequency = budget.FrequencyId ?? 0;
            while (due <= today && frequency != 0)
            {
                var next = BudgetScheduleService.CalculateNextDueDate(due.ToDateTime(TimeOnly.MinValue), frequency);
                if (DateOnly.FromDateTime(next) <= due) break;
                due = DateOnly.FromDateTime(next);
            }
            while (due > today && due <= end && (!HasEndDate(budget) || due <= DateOnly.FromDateTime(budget.EndDate!.Value)))
            {
                var amount = budget.BudgetTypeId == 0 ? budget.Amount ?? 0m : -(budget.Amount ?? 0m);
                occurrences.Add((due, budget.BudgetId, budget.BudgetName ?? "Budget item", CurrencyPolicy.Round(amount)));
                if (frequency == 0) break;
                var next = BudgetScheduleService.CalculateNextDueDate(due.ToDateTime(TimeOnly.MinValue), frequency);
                if (DateOnly.FromDateTime(next) <= due) break;
                due = DateOnly.FromDateTime(next);
            }
        }

        var points = new List<CashflowPoint> { new(today, "Starting balance", 0m, starting, true) };
        var balance = starting;
        foreach (var occurrence in occurrences.OrderBy(o => o.Date).ThenBy(o => o.Id))
        {
            balance = CurrencyPolicy.Round(balance + occurrence.Amount);
            points.Add(new(occurrence.Date, occurrence.Name, occurrence.Amount, balance));
        }
        var low = points.OrderBy(p => p.Balance).ThenBy(p => p.Date).First();
        return new(today, end, starting, points, low.Balance, low.Date);
    }

    public async Task<NetWorthReport> GetNetWorthAsync(int userId, DateOnly selectedMonth, DateOnly? asOf = null)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.Today);
        var readable = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var accounts = await ReadableAccounts(userId, readable).ToListAsync();
        var accountIds = accounts.Select(a => a.AccountId).ToList();
        var transactions = await ReadableTransactions(userId, readable)
            .Where(t => accountIds.Contains(t.AccountId))
            .Select(t => new { t.AccountId, t.TransactionDate, t.Amount })
            .ToListAsync();
        decimal At(DateOnly date) => CurrencyPolicy.Round(accounts.Sum(a => a.BeginningBalance +
            transactions.Where(t => t.AccountId == a.AccountId && t.TransactionDate <= date).Sum(t => t.Amount)));

        var month = FirstOfMonth(selectedMonth);
        var history = Enumerable.Range(0, 7).Select(i => month.AddMonths(i - 6))
            .Select(m => m.AddMonths(1).AddDays(-1))
            .Select(d => new NetWorthPoint(d, At(d))).ToList();
        return new(At(today), today, history);
    }

    private IQueryable<Account> ReadableAccounts(int userId, IReadOnlyCollection<int> readable) =>
        _db.Accounts.AsNoTracking().Where(a => !(a.IsDeleted ?? false) && (a.SharedBudgetId.HasValue
            ? readable.Contains(a.SharedBudgetId.Value) : a.UserId == userId));

    private IQueryable<Transaction> ReadableTransactions(int userId, IReadOnlyCollection<int> readable) =>
        _db.Transactions.AsNoTracking().Where(t =>
            (t.SharedBudgetId.HasValue ? readable.Contains(t.SharedBudgetId.Value) : t.UserId == userId) &&
            t.Account != null && (t.Account.SharedBudgetId.HasValue
                ? readable.Contains(t.Account.SharedBudgetId.Value) : t.Account.UserId == userId));

    private static string CategoryName(Category? category, int userId, int? sharedBudgetId, IReadOnlyCollection<int> readable)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.CategoryName)) return InsightsDataService.UncategorizedCategoryName;
        if (sharedBudgetId.HasValue ? category.SharedBudgetId != sharedBudgetId || !readable.Contains(sharedBudgetId.Value) : category.UserId != userId)
            return InsightsDataService.UncategorizedCategoryName;
        return category.CategoryName.Trim();
    }

    private static bool HasEndDate(Budget budget) => budget.EndDate.HasValue && budget.EndDate.Value != NoEndDate;
    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
