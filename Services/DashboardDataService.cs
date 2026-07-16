using ClintonFrankland.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ClintonFrankland.Services;

public class DashboardDataService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly CheckbookDataService _checkbook;
    private readonly BudgetScheduleService _budgetSchedule;
    private readonly BudgetAllowanceService _budgetAllowance;

    [ActivatorUtilitiesConstructor]
    public DashboardDataService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _checkbook = null!;
        _budgetSchedule = null!;
        _budgetAllowance = null!;
    }

    internal DashboardDataService(
        CheckbookDataService checkbook,
        BudgetScheduleService budgetSchedule,
        BudgetAllowanceService budgetAllowance)
    {
        _checkbook = checkbook;
        _budgetSchedule = budgetSchedule;
        _budgetAllowance = budgetAllowance;
    }

    public async Task<DashboardSnapshotViewModel> GetSnapshotAsync(int userId, DateTime today)
    {
        if (_scopeFactory is null)
            return await BuildSnapshotAsync(_checkbook, _budgetSchedule, _budgetAllowance, userId, today);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var checkbook = scope.ServiceProvider.GetRequiredService<CheckbookDataService>();
        var budgetSchedule = scope.ServiceProvider.GetRequiredService<BudgetScheduleService>();
        var budgetAllowance = scope.ServiceProvider.GetRequiredService<BudgetAllowanceService>();

        return await BuildSnapshotAsync(checkbook, budgetSchedule, budgetAllowance, userId, today);
    }

    private static async Task<DashboardSnapshotViewModel> BuildSnapshotAsync(
        CheckbookDataService checkbook,
        BudgetScheduleService budgetSchedule,
        BudgetAllowanceService budgetAllowance,
        int userId,
        DateTime today)
    {
        var snapshot = new DashboardSnapshotViewModel
        {
            AsOfDate = today.Date
        };

        // Today's balance: default ledger opening balance plus posted transactions.
        snapshot.TodayBalance = await checkbook.GetCurrentBalanceAsync(userId);

        // Upcoming bills: budgets flagged as bills, due within next 14 days (default snapshot window)
        var budgets = await checkbook.GetBudgetsForUserAsync(userId);

        var dueThrough = today.Date.AddDays(14);
        var upcomingBills = budgets
            .Where(b => b.IsBill == true)
            .Select(b => new
            {
                b.BudgetId,
                Name = b.BudgetName ?? string.Empty,
                Payee = b.Payee?.PayeeName ?? string.Empty,
                DueDate = (b.NextDueDate ?? today).Date,
                Amount = (b.Amount ?? 0m)
            })
            .Where(b => b.DueDate <= dueThrough)
            .OrderBy(b => b.DueDate)
            .ThenBy(b => b.Name)
            .ToList();

        snapshot.UpcomingBills = upcomingBills
            .Take(8)
            .Select(b => new DashboardBillItemViewModel
            {
                BudgetId = b.BudgetId,
                Name = string.IsNullOrWhiteSpace(b.Name) ? $"Bill #{b.BudgetId}" : b.Name,
                Payee = b.Payee,
                DueDate = b.DueDate,
                Amount = b.Amount,
                IsPastDue = b.DueDate < today.Date
            })
            .ToList();

        // Upcoming bills total (next 7 days)
        var next7 = today.Date.AddDays(7);
        var upcoming7Total = upcomingBills
            .Where(b => b.DueDate <= next7)
            .Sum(b => b.Amount);

        snapshot.UpcomingBillsTotal = upcoming7Total;

        // Monthly spend by category (expenses only, current month, top 8)
        var monthlyExpenses = await checkbook.GetMonthlyExpensesByCategoryAsync(userId, today.Year, today.Month);

        var monthlyBudgetByCategory = budgets
            .Where(b => b.IsSpendingAllowance && b.Amount.HasValue && (b.FrequencyId ?? 0) != 0)
            .GroupBy(b => b.Category?.CategoryName ?? "Uncategorized")
            .ToDictionary(
                g => g.Key,
                g => g.Sum(b => CurrencyPolicy.Round((b.Amount ?? 0m) * GetMonthlyMultiplier(b.FrequencyId ?? 4))));
        var monthlyExpensesByCategory = monthlyExpenses
            .ToDictionary(c => c.Category, c => c.Total, StringComparer.OrdinalIgnoreCase);
        var allowanceProgress = await budgetAllowance.GetCurrentProgressAsync(
            userId,
            budgets,
            DateOnly.FromDateTime(today));
        var allowanceCategories = budgets
            .Where(b => allowanceProgress.ContainsKey(b.BudgetId))
            .GroupBy(b => b.Category?.CategoryName ?? "Uncategorized", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var progress = group.Select(b => allowanceProgress[b.BudgetId]).ToList();
                monthlyExpensesByCategory.TryGetValue(group.Key, out var monthlyTotal);
                monthlyBudgetByCategory.TryGetValue(group.Key, out var monthlyBudget);
                return new DashboardCategorySpendViewModel
                {
                    CategoryName = group.Key,
                    Total = monthlyTotal,
                    BudgetedMonthly = monthlyBudget > 0m ? monthlyBudget : null,
                    AllowancePlanned = CurrencyPolicy.Round(progress.Sum(p => p.PlannedAmount)),
                    AllowanceSpent = CurrencyPolicy.Round(progress.Sum(p => p.SpentAmount)),
                    AllowanceRemaining = CurrencyPolicy.Round(progress.Sum(p => p.RemainingAmount)),
                    AllowanceResetDate = progress.Min(p => p.PeriodEnd).ToDateTime(TimeOnly.MinValue)
                };
            })
            .ToList();
        var allowanceNames = allowanceCategories.Select(c => c.CategoryName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        snapshot.CategorySpend = allowanceCategories
            .Concat(monthlyExpenses
                .Where(c => !allowanceNames.Contains(c.Category))
                .Select(c => new DashboardCategorySpendViewModel
                {
                    CategoryName = c.Category,
                    Total = c.Total
                }))
            .OrderByDescending(c => c.HasAllowance)
            .ThenByDescending(c => c.HasAllowance ? c.AllowanceSpent : c.Total)
            .ThenBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Calculate lowest projected balance over next 6 months
        var (lowestBalance, lowestDate) = await budgetSchedule.GetLowestProjectedBalanceAsync(
            userId, today, today.AddMonths(6));

        snapshot.LowestProjectedBalance = lowestBalance;
        snapshot.LowestProjectedBalanceDate = lowestDate;

        return snapshot;
    }

    private static decimal GetMonthlyMultiplier(int frequencyId) => frequencyId switch
    {
        1 => 52m / 12m,        // Weekly
        2 => 26m / 12m,        // Bi-weekly
        4 => 1m,               // Monthly
        5 => 1m / 2m,          // Bi-monthly (every 2 months)
        6 => 1m / 3m,          // Quarterly
        7 => 365m / 35m / 12m, // Every 5 weeks
        8 => 2m,               // Semi-monthly
        9 => 1m / 12m,         // Yearly
        10 => 365m / 5m / 12m,  // Every 5 days
        11 => 365m / 42m / 12m, // Every 6 weeks
        12 => 365m / 21m / 12m, // Every 3 weeks
        13 => 365m / 28m / 12m, // Every 4 weeks
        14 => 1m / 6m,          // Semi-annually
        _ => 1m
    };

}
