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

        // Upcoming bills total (next 7 days) and safe-to-spend (very simple heuristic)
        var next7 = today.Date.AddDays(7);
        var upcoming7Total = upcomingBills
            .Where(b => b.DueDate <= next7)
            .Sum(b => b.Amount);

        snapshot.UpcomingBillsTotal = upcoming7Total;

        // Safe-to-spend: current balance minus near-term bills (next 7 days).
        // This is intentionally simple for a first snapshot.
        snapshot.SafeToSpend = snapshot.TodayBalance - upcoming7Total;

        return snapshot;
    }
}
