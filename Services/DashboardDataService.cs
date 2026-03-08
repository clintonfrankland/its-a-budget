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
            .Where(i => i.Date >= startDate)
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
