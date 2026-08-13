using ClintonFrankland.Data;
using ClintonFrankland.Models;
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
        var allowances = await _db.Budgets.AsNoTracking().Include(b => b.Category)
            .Where(b => b.IsSpendingAllowance && (b.SharedBudgetId.HasValue
                ? readable.Contains(b.SharedBudgetId.Value)
                : b.UserId == userId))
            .ToListAsync();
        var transactions = await ReadableTransactions(userId, readable)
            .Include(t => t.Category)
            .Where(t => t.Amount < 0 && t.TransactionDate >= start && t.TransactionDate < end)
            .ToListAsync();

        var legacyPlanned = targets
            .GroupBy(t => PlanScope(t.CategoryId, t.SharedBudgetId, t.UserId))
            .ToDictionary(
                g => g.Key,
                g => new PlanValue(
                    CategoryName(g.First().Category, userId, g.First().SharedBudgetId, readable),
                    CurrencyPolicy.Round(g.Sum(t => t.PlannedAmount))));
        var allowancePlanned = allowances
            .Select(b => new
            {
                Scope = PlanScope(b.CategoryId, b.SharedBudgetId, b.UserId),
                Name = CategoryName(b.Category, userId, b.SharedBudgetId, readable),
                Amount = GetAllowancePlanForMonth(b, start, end)
            })
            .Where(x => x.Amount > 0m)
            .GroupBy(x => x.Scope)
            .ToDictionary(
                g => g.Key,
                g => new PlanValue(g.First().Name, CurrencyPolicy.Round(g.Sum(x => x.Amount))));
        var planned = legacyPlanned
            .Where(x => x.Value.Amount > 0m && !allowancePlanned.ContainsKey(x.Key))
            .Select(x => x.Value)
            .Concat(allowancePlanned.Values)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => CurrencyPolicy.Round(g.Sum(x => x.Amount)), StringComparer.OrdinalIgnoreCase);
        var actual = transactions.GroupBy(t => CategoryName(t.Category, userId, t.SharedBudgetId, readable), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => CurrencyPolicy.Round(g.Sum(t => -t.Amount)), StringComparer.OrdinalIgnoreCase);

        return MonthCloseVarianceSnapshot.Create(selectedMonth, planned.Keys.Union(actual.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(name => new SpendPlanRow(name, planned.GetValueOrDefault(name), actual.GetValueOrDefault(name)))
            .ToList()).Rows.ToList();
    }

    public async Task<MonthCloseVarianceSnapshot> GetMonthCloseVarianceAsync(int userId, DateOnly selectedMonth) =>
        MonthCloseVarianceSnapshot.Create(selectedMonth, await GetSpendVsPlanAsync(userId, selectedMonth));

    public async Task<MonthlyReport> GetMonthlyReportAsync(int userId, DateOnly selectedMonth)
    {
        var start = FirstOfMonth(selectedMonth);
        var end = start.AddMonths(1);
        var readable = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var budgets = await _db.Budgets.AsNoTracking().Include(b => b.Category)
            .Where(b => b.SharedBudgetId.HasValue ? readable.Contains(b.SharedBudgetId.Value) : b.UserId == userId)
            .ToListAsync();
        var transactions = await ReadableTransactions(userId, readable).Include(t => t.Category).Include(t => t.Payee).Include(t => t.Account)
            .Where(t => t.TransactionDate >= start && t.TransactionDate < end).ToListAsync();

        var incomeBudgets = budgets.Where(b => !b.IsSpendingAllowance && b.BudgetTypeId == 0).ToList();
        var billBudgets = budgets.Where(b => !b.IsSpendingAllowance && b.BudgetTypeId != 0).ToList();
        var allowanceBudgets = budgets.Where(b => b.IsSpendingAllowance).ToList();
        var income = BuildPlannedLines(incomeBudgets, start, end, MonthlyReportLineKind.Income, userId, readable);
        var bills = BuildPlannedLines(billBudgets, start, end, MonthlyReportLineKind.Bill, userId, readable);
        var allowances = BuildPlannedLines(allowanceBudgets, start, end, MonthlyReportLineKind.Allowance, userId, readable);
        var billScopes = billBudgets.Select(b => PlanScope(b.CategoryId, b.SharedBudgetId, b.UserId)).ToHashSet();
        var allowanceScopes = allowanceBudgets.Select(b => PlanScope(b.CategoryId, b.SharedBudgetId, b.UserId)).ToHashSet();
        var transfers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var transaction in transactions)
        {
            var name = CategoryName(transaction.Category, userId, transaction.SharedBudgetId, readable);
            if (IsTransferOrCardPayment(transaction))
            {
                AddActual(transfers, name, Math.Abs(transaction.Amount));
                continue;
            }
            if (transaction.Amount > 0m)
                income = AddActual(income, name, transaction.Amount, MonthlyReportLineKind.Income);
            else
            {
                var scope = PlanScope(transaction.CategoryId, transaction.SharedBudgetId, transaction.UserId);
                var target = billScopes.Contains(scope) ? bills : allowances;
                // Credit-card purchases remain spending: only an explicitly labelled card payment is a transfer.
                target = AddActual(target, name, -transaction.Amount, target == bills ? MonthlyReportLineKind.Bill : MonthlyReportLineKind.Allowance);
                if (target == bills) bills = target; else allowances = target;
            }
        }

        var transferLines = transfers.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new MonthlyReportLine(x.Key, 0m, CurrencyPolicy.Round(x.Value), MonthlyReportLineKind.Transfer)).ToList();
        var plannedNet = CurrencyPolicy.Round(income.Sum(x => x.Planned) - bills.Sum(x => x.Planned) - allowances.Sum(x => x.Planned));
        var actualNet = CurrencyPolicy.Round(income.Sum(x => x.Actual) - bills.Sum(x => x.Actual) - allowances.Sum(x => x.Actual));
        return new(start, income, bills, allowances, transferLines, plannedNet, actualNet);
    }

    private static List<MonthlyReportLine> BuildPlannedLines(IEnumerable<Budget> budgets, DateOnly start, DateOnly end, MonthlyReportLineKind kind, int userId, IReadOnlyCollection<int> readable)
    {
        return budgets.Select(b => new { Budget = b, Amount = OccurrencesInMonth(b, start, end) * (b.Amount ?? 0m) })
            .Where(x => x.Amount != 0m).GroupBy(x => CategoryName(x.Budget.Category, userId, x.Budget.SharedBudgetId, readable), StringComparer.OrdinalIgnoreCase)
            .Select(g => new MonthlyReportLine(g.Key, CurrencyPolicy.Round(g.Sum(x => x.Amount)), 0m, kind))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<MonthlyReportLine> AddActual(List<MonthlyReportLine> lines, string name, decimal amount, MonthlyReportLineKind kind)
    {
        var line = lines.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (line >= 0) lines[line] = lines[line] with { Actual = CurrencyPolicy.Round(lines[line].Actual + amount) };
        else lines.Add(new MonthlyReportLine(name, 0m, CurrencyPolicy.Round(amount), kind));
        return lines;
    }

    private static void AddActual(IDictionary<string, decimal> values, string name, decimal amount)
    {
        values.TryGetValue(name, out var existing);
        values[name] = CurrencyPolicy.Round(existing + amount);
    }

    private static int OccurrencesInMonth(Budget budget, DateOnly start, DateOnly end)
    {
        if (!budget.NextDueDate.HasValue) return 0;
        var occurrence = DateOnly.FromDateTime(budget.NextDueDate.Value);
        var frequency = budget.FrequencyId ?? 0;
        if (frequency == 0) return occurrence >= start && occurrence < end ? 1 : 0;
        // Move from the persisted next date to the selected month; this preserves actual weekly/biweekly occurrence counts.
        var guard = 0;
        while (occurrence >= end && guard++ < 240) occurrence = PreviousOccurrence(occurrence, frequency);
        while (guard++ < 480)
        {
            var previous = PreviousOccurrence(occurrence, frequency);
            if (previous < start || previous >= occurrence) break;
            occurrence = previous;
        }
        while (occurrence < start && guard++ < 480)
        {
            var next = DateOnly.FromDateTime(BudgetScheduleService.CalculateNextDueDate(occurrence.ToDateTime(TimeOnly.MinValue), frequency));
            if (next <= occurrence) return 0;
            occurrence = next;
        }
        var count = 0;
        while (occurrence < end && guard++ < 720)
        {
            if ((!HasEndDate(budget) || occurrence <= DateOnly.FromDateTime(budget.EndDate!.Value)) && occurrence >= start) count++;
            var next = DateOnly.FromDateTime(BudgetScheduleService.CalculateNextDueDate(occurrence.ToDateTime(TimeOnly.MinValue), frequency));
            if (next <= occurrence) break;
            occurrence = next;
        }
        return count;
    }

    private static DateOnly PreviousOccurrence(DateOnly date, int frequency) => frequency switch
    {
        1 => date.AddDays(-7), 2 => date.AddDays(-14), 4 => date.AddMonths(-1), 5 => date.AddMonths(-2), 6 => date.AddMonths(-3),
        7 => date.AddDays(-35), 8 => date.Day <= 15 ? new DateOnly(date.Year, date.Month, 1).AddDays(-1) : new DateOnly(date.Year, date.Month, 15),
        9 => date.AddYears(-1), 10 => date.AddDays(-5), 11 => date.AddDays(-42), 12 => date.AddDays(-21), 13 => date.AddDays(-28), 14 => date.AddMonths(-6), _ => date
    };

    private static bool IsTransferOrCardPayment(Transaction transaction)
    {
        var text = $"{transaction.Category?.CategoryName} {transaction.Payee?.PayeeName} {transaction.Notes}";
        return text.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
            (text.Contains("credit card", StringComparison.OrdinalIgnoreCase) && text.Contains("payment", StringComparison.OrdinalIgnoreCase));
    }

    public Task<List<CategoryTrendRow>> GetCategoryTrendsAsync(int userId, DateOnly selectedMonth) =>
        new InsightsDataService(_db, _sharedBudgets).GetCategoryTrendAsync(userId, selectedMonth);

    private static decimal GetAllowancePlanForMonth(Budget budget, DateOnly monthStart, DateOnly monthEnd)
    {
        var frequency = budget.FrequencyId ?? 0;
        if (frequency == 0)
            return 0m;

        var periodStart = BudgetAllowanceService.GetEffectiveStart(budget);
        var periodEnd = DateOnly.FromDateTime(
            BudgetScheduleService.CalculateNextDueDate(periodStart.ToDateTime(TimeOnly.MinValue), frequency));
        if (periodStart >= monthEnd)
            return 0m;

        while (periodEnd <= monthStart)
        {
            periodStart = periodEnd;
            var next = DateOnly.FromDateTime(BudgetScheduleService.CalculateNextDueDate(periodEnd.ToDateTime(TimeOnly.MinValue), frequency));
            if (next <= periodEnd)
                return 0m;
            periodEnd = next;
        }

        var total = 0m;
        while (periodStart < monthEnd && (!HasEndDate(budget) || periodStart <= DateOnly.FromDateTime(budget.EndDate!.Value)))
        {
            if (periodStart >= monthStart)
                total += budget.Amount ?? 0m;

            periodStart = periodEnd;
            var next = DateOnly.FromDateTime(BudgetScheduleService.CalculateNextDueDate(periodEnd.ToDateTime(TimeOnly.MinValue), frequency));
            if (next <= periodEnd) break;
            periodEnd = next;
        }

        return CurrencyPolicy.Round(total);
    }

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
        var ledgerAccount = accounts
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.AccountId)
            .FirstOrDefault();
        var starting = ledgerAccount is null
            ? 0m
            : AccountBalancePresentationPolicy.Apply(
                ledgerAccount.AccountTypeId,
                CurrencyPolicy.Round(ledgerAccount.BeginningBalance + posted));
        var forecast = await new BudgetScheduleService(_db, _sharedBudgets)
            .GetForecastAsync(
                userId,
                today.ToDateTime(TimeOnly.MinValue),
                end.ToDateTime(TimeOnly.MinValue),
                includeEndDate: true);
        var occurrences = forecast
            .Where(i => i.DueDate.Date > today.ToDateTime(TimeOnly.MinValue).Date)
            .Select(i => (Date: DateOnly.FromDateTime(i.DueDate), Id: i.BudgetId, Name: i.BudgetName, i.Amount))
            .ToList();

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
        decimal At(DateOnly date) => CurrencyPolicy.Round(accounts.Sum(account =>
            AccountBalancePresentationPolicy.Apply(
                account.AccountTypeId,
                CurrencyPolicy.Round(account.BeginningBalance + transactions
                    .Where(transaction => transaction.AccountId == account.AccountId && transaction.TransactionDate <= date)
                    .Sum(transaction => transaction.Amount)))));

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
        _db.ReadableTransactions(userId, readable);

    private static string CategoryName(Category? category, int userId, int? sharedBudgetId, IReadOnlyCollection<int> readable)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.CategoryName)) return InsightsDataService.UncategorizedCategoryName;
        if (sharedBudgetId.HasValue ? category.SharedBudgetId != sharedBudgetId || !readable.Contains(sharedBudgetId.Value) : category.UserId != userId)
            return InsightsDataService.UncategorizedCategoryName;
        return category.CategoryName.Trim();
    }

    private static bool HasEndDate(Budget budget) => budget.EndDate.HasValue && budget.EndDate.Value != NoEndDate;
    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
    private static PlanScopeKey PlanScope(int categoryId, int? sharedBudgetId, int? userId) =>
        new(categoryId, sharedBudgetId, sharedBudgetId.HasValue ? 0 : userId ?? 0);
    private sealed record PlanScopeKey(int CategoryId, int? SharedBudgetId, int OwnerUserId);
    private sealed record PlanValue(string Name, decimal Amount);
}
