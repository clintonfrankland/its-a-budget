using ClintonFrankland.Models;

namespace ClintonFrankland.Services;

public class DashboardDataService
{
    private readonly CheckbookDataService _checkbook;

    public DashboardDataService(CheckbookDataService checkbook)
    {
        _checkbook = checkbook;
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
                g => g.Sum(b => Math.Round((b.Amount ?? 0m) * GetMonthlyMultiplier(b.FrequencyId ?? 4), 2)));

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
        var (lowestBalance, lowestDate) = CalculateLowestProjectedBalance(
            snapshot.TodayBalance, budgets, today, today.AddMonths(6));

        snapshot.LowestProjectedBalance = lowestBalance;
        snapshot.LowestProjectedBalanceDate = lowestDate;

        return snapshot;
    }

    private static (decimal LowestBalance, DateTime LowestDate) CalculateLowestProjectedBalance(
        decimal currentBalance,
        List<Models.Entities.Budget> budgets,
        DateTime startDate,
        DateTime endDate)
    {
        // Project all budget items forward through the date range
        var projectedItems = new List<(DateTime Date, decimal Amount)>();

        foreach (var budget in budgets)
        {
            var nextDue = budget.NextDueDate ?? startDate;
            var budgetEndDate = (budget.EndDate == null || budget.EndDate == DateTime.Parse("1970-01-01"))
                ? endDate
                : budget.EndDate.Value;
            var frequencyId = budget.FrequencyId ?? 0;

            // Project this budget forward until end date
            while (nextDue <= endDate && nextDue <= budgetEndDate)
            {
                // Income (BudgetTypeId = 0) is positive, expenses are negative
                var amount = budget.BudgetTypeId == 0 
                    ? (budget.Amount ?? 0m) 
                    : -(budget.Amount ?? 0m);

                projectedItems.Add((nextDue, amount));

                // Calculate next due date
                if (frequencyId == 0) break; // One-time
                nextDue = CalculateNextDueDate(nextDue, frequencyId);
            }
        }

        // Sort by date and calculate running balance to find the lowest point
        var sortedItems = projectedItems
            .OrderBy(i => i.Date)
            .ThenByDescending(i => i.Amount) // Process income before expenses on same day
            .ToList();

        var runningBalance = currentBalance;
        var lowestBalance = currentBalance;
        var lowestDate = startDate;

        foreach (var item in sortedItems)
        {
            runningBalance += item.Amount;
            if (runningBalance < lowestBalance)
            {
                lowestBalance = runningBalance;
                lowestDate = item.Date;
            }
        }

        return (lowestBalance, lowestDate);
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

    private static DateTime CalculateNextDueDate(DateTime currentDate, int frequencyId)
    {
        return frequencyId switch
        {
            0 => currentDate, // One-time - no change
            1 => currentDate.AddDays(7), // Weekly
            2 => currentDate.AddDays(14), // Bi-weekly
            4 => currentDate.AddMonths(1), // Monthly
            5 => currentDate.AddMonths(2), // Bi-monthly
            6 => currentDate.AddMonths(3), // Quarterly
            7 => currentDate.AddDays(35), // 5 weeks
            8 => CalculateSemiMonthly(currentDate), // Semi-monthly (1st and 15th)
            9 => currentDate.AddYears(1), // Yearly
            10 => currentDate.AddDays(5), // Every 5 days
            11 => currentDate.AddDays(42), // 6 weeks
            12 => currentDate.AddDays(21), // 3 weeks
            13 => currentDate.AddDays(28), // 4 weeks
            14 => currentDate.AddMonths(6), // Semi-annually
            _ => currentDate.AddMonths(1) // Default to monthly
        };
    }

    private static DateTime CalculateSemiMonthly(DateTime currentDate)
    {
        // If on 1st, go to 15th; otherwise go to 1st of next month
        if (currentDate.Day == 1)
            return currentDate.AddDays(14);
        else
            return new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1);
    }
}
