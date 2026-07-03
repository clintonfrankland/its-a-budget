using ClintonFrankland.Models;

namespace ClintonFrankland.Services;

public class DashboardDataService
{
    private readonly CheckbookDataService _checkbook;
    private readonly BudgetScheduleService _budgetSchedule;

    public DashboardDataService(CheckbookDataService checkbook, BudgetScheduleService budgetSchedule)
    {
        _checkbook = checkbook;
        _budgetSchedule = budgetSchedule;
    }

    public async Task<DashboardSnapshotViewModel> GetSnapshotAsync(int userId, DateTime today)
    {
        var snapshot = new DashboardSnapshotViewModel
        {
            AsOfDate = today.Date
        };

        // Today's balance: BeginningBalance + sum(all transactions)
        var account = await _checkbook.GetAccountForUserAsync(userId);
        var startingBalance = account?.BeginningBalance ?? 0m;
        var transactionSum = await _checkbook.GetTransactionSumAsync(userId);
        snapshot.TodayBalance = startingBalance + transactionSum;

        // Upcoming bills: budgets flagged as bills, due within next 14 days (default snapshot window)
        var budgets = await _checkbook.GetBudgetsForUserAsync(userId);

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
        var monthlyExpenses = await _checkbook.GetMonthlyExpensesByCategoryAsync(userId, today.Year, today.Month);

        var monthlyBudgetByCategory = budgets
            .Where(b => b.BudgetTypeId != 0 && b.Amount.HasValue && (b.FrequencyId ?? 0) != 0)
            .GroupBy(b => b.Category?.CategoryName ?? "Uncategorized")
            .ToDictionary(
                g => g.Key,
                g => g.Sum(b => CurrencyPolicy.Round((b.Amount ?? 0m) * GetMonthlyMultiplier(b.FrequencyId ?? 4))));

        snapshot.CategorySpend = monthlyExpenses
            .Select(c =>
            {
                monthlyBudgetByCategory.TryGetValue(c.Category, out var budgeted);
                return new DashboardCategorySpendViewModel
                {
                    CategoryName = c.Category,
                    Total = c.Total,
                    BudgetedMonthly = budgeted > 0 ? budgeted : null
                };
            })
            .ToList();

        // Calculate lowest projected balance over next 6 months
        var (lowestBalance, lowestDate) = await _budgetSchedule.GetLowestProjectedBalanceAsync(
            userId, today, today.AddMonths(6));

        snapshot.LowestProjectedBalance = lowestBalance;
        snapshot.LowestProjectedBalanceDate = lowestDate;

        return snapshot;
    }

    private static decimal GetMonthlyMultiplier(int frequencyId) => frequencyId switch
    {
        1  => 52m / 12m,        // Weekly
        2  => 26m / 12m,        // Bi-weekly
        4  => 1m,               // Monthly
        5  => 1m / 2m,          // Bi-monthly (every 2 months)
        6  => 1m / 3m,          // Quarterly
        7  => 365m / 35m / 12m, // Every 5 weeks
        8  => 2m,               // Semi-monthly
        9  => 1m / 12m,         // Yearly
        10 => 365m / 5m / 12m,  // Every 5 days
        11 => 365m / 42m / 12m, // Every 6 weeks
        12 => 365m / 21m / 12m, // Every 3 weeks
        13 => 365m / 28m / 12m, // Every 4 weeks
        14 => 1m / 6m,          // Semi-annually
        _  => 1m
    };

}
