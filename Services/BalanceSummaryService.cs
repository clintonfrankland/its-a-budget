using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;

namespace ClintonFrankland.Services;

public sealed class BalanceSummaryService
{
    private readonly CheckbookDataService _checkbook;
    private readonly BudgetScheduleService _schedule;
    private readonly BudgetItemsDataService _budgetItems;

    public BalanceSummaryService(
        CheckbookDataService checkbook,
        BudgetScheduleService schedule,
        BudgetItemsDataService budgetItems)
    {
        _checkbook = checkbook;
        _schedule = schedule;
        _budgetItems = budgetItems;
    }

    public async Task<BalanceSummaryViewModel> GetAsync(int userId, DateTime today)
    {
        var balance = await _checkbook.GetCurrentBalanceAsync(userId);
        var cleared = await _checkbook.GetClearedBalanceAsync(userId);
        var (safeToSpend, safeToSpendDate) = await _schedule.GetLowestProjectedBalanceAsync(
            userId, today.Date, today.Date.AddMonths(6));
        var budgets = await _budgetItems.GetBudgetsForUserAsync(userId);
        var monthlyPlan = CurrencyPolicy.Round(budgets.Sum(CalculateMonthlyAmount));

        return new BalanceSummaryViewModel(
            balance,
            cleared,
            safeToSpend,
            safeToSpendDate,
            monthlyPlan);
    }

    public static decimal CalculateMonthlyAmount(Budget budget)
    {
        var amount = budget.Amount ?? 0m;
        var frequencyId = budget.FrequencyId ?? 0;
        var multiplier = budget.BudgetTypeId == 0 ? 1m : -1m;
        var monthly = frequencyId switch
        {
            1 => amount * 52m / 12m,
            2 => amount * 26m / 12m,
            4 => amount,
            5 => amount / 2m,
            6 => amount / 3m,
            7 => amount * 52m / 5m / 12m,
            8 => amount * 2m,
            9 => amount / 12m,
            10 => amount * 73m / 12m,
            11 => amount * 52m / 6m / 12m,
            12 => amount * 52m / 3m / 12m,
            13 => amount * 52m / 4m / 12m,
            14 => amount / 6m,
            _ => 0m
        };

        return CurrencyPolicy.Round(monthly * multiplier);
    }
}
