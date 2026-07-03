using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class BudgetScheduleService
{
    private static readonly DateTime NoEndDate = new(1970, 1, 1);

    private readonly ClintonFranklandDbContext _db;

    public BudgetScheduleService(ClintonFranklandDbContext db)
    {
        _db = db;
    }

    public async Task<List<BudgetItemViewModel>> GetForecastAsync(int userId, DateTime endDate, bool includeEndDate = false)
    {
        var startingBalance = await GetCurrentBalanceAsync(userId);
        var budgets = await GetUserBudgetsWithLookupsAsync(userId);

        var incomeBudget = budgets
            .Where(b => b.BudgetTypeId == 0)
            .OrderBy(b => b.NextDueDate)
            .FirstOrDefault();

        if (incomeBudget?.NextDueDate > endDate)
            endDate = incomeBudget.NextDueDate.Value;

        return ProjectForecast(startingBalance, budgets, DateTime.Today, endDate, includeEndDate);
    }

    public async Task<(decimal LowestBalance, DateTime LowestDate)> GetLowestProjectedBalanceAsync(
        int userId,
        DateTime startDate,
        DateTime endDate)
    {
        var currentBalance = await GetCurrentBalanceAsync(userId);
        var budgets = await GetUserBudgetsWithLookupsAsync(userId);
        var forecast = ProjectForecast(currentBalance, budgets, startDate, endDate, includeEndDate: true);

        var lowest = forecast
            .OrderBy(i => i.Balance)
            .ThenBy(i => i.DueDate)
            .FirstOrDefault();

        if (lowest is null || lowest.Balance >= currentBalance)
            return (currentBalance, startDate);

        return (lowest.Balance, lowest.DueDate);
    }

    public async Task MarkBudgetPaidAsync(int userId, int budgetId)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);
        if (budget is null)
            return;

        var newNextDueDate = CalculateNextDueDate(budget.NextDueDate ?? DateTime.Today, budget.FrequencyId ?? 0);

        if (ShouldDeleteAfterAdvance(budget, newNextDueDate))
        {
            _db.Budgets.Remove(budget);
        }
        else
        {
            budget.NextDueDate = newNextDueDate;
        }

        await _db.SaveChangesAsync();
    }

    public async Task CreateEditedNextOccurrenceAsync(
        int userId,
        int originalBudgetId,
        string budgetName,
        DateTime dueDate,
        decimal amount,
        string categoryName,
        bool isAutomatic,
        bool isLate)
    {
        if (string.IsNullOrWhiteSpace(budgetName))
            throw new InvalidOperationException("Budget name is required.");

        if (string.IsNullOrWhiteSpace(categoryName))
            throw new InvalidOperationException("Category is required.");

        if (dueDate == DateTime.MinValue)
            throw new InvalidOperationException("Due date is required.");

        var roundedAmount = CurrencyPolicy.RoundNonNegativeSqlAmount(amount);

        var originalBudget = await _db.Budgets
            .Include(b => b.Payee)
            .FirstOrDefaultAsync(b => b.BudgetId == originalBudgetId && b.UserId == userId);

        if (originalBudget is null)
            return;

        var categoryId = await GetOrCreateCategoryAsync(categoryName.Trim(), userId);
        var payeeId = await GetOrCreatePayeeAsync(originalBudget.Payee?.PayeeName ?? string.Empty, userId);
        var newNextDueDate = CalculateNextDueDate(originalBudget.NextDueDate ?? DateTime.Today, originalBudget.FrequencyId ?? 0);

        if (ShouldDeleteAfterAdvance(originalBudget, newNextDueDate))
        {
            _db.Budgets.Remove(originalBudget);
        }
        else
        {
            originalBudget.NextDueDate = newNextDueDate;
        }

        _db.Budgets.Add(new Budget
        {
            BudgetName = budgetName.Trim(),
            BudgetTypeId = originalBudget.BudgetTypeId,
            FrequencyId = 0,
            NextDueDate = dueDate,
            EndDate = NoEndDate,
            Amount = roundedAmount,
            CategoryId = categoryId,
            UserId = userId,
            IsAutomatic = isAutomatic,
            IsBill = originalBudget.IsBill,
            IsLate = isLate,
            PayeeId = payeeId > 0 ? payeeId : null
        });

        await _db.SaveChangesAsync();
    }

    public static DateTime CalculateNextDueDate(DateTime currentDate, int frequencyId)
    {
        return frequencyId switch
        {
            0 => currentDate,
            1 => currentDate.AddDays(7),
            2 => currentDate.AddDays(14),
            4 => currentDate.AddMonths(1),
            5 => currentDate.AddMonths(2),
            6 => currentDate.AddMonths(3),
            7 => currentDate.AddDays(35),
            8 => CalculateSemiMonthly(currentDate),
            9 => currentDate.AddYears(1),
            10 => currentDate.AddDays(5),
            11 => currentDate.AddDays(42),
            12 => currentDate.AddDays(21),
            13 => currentDate.AddDays(28),
            14 => currentDate.AddMonths(6),
            _ => currentDate
        };
    }

    private async Task<decimal> GetCurrentBalanceAsync(int userId)
    {
        var account = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId);
        var transactionSum = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return (account?.BeginningBalance ?? 0m) + transactionSum;
    }

    private Task<List<Budget>> GetUserBudgetsWithLookupsAsync(int userId) =>
        _db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Frequency)
            .Include(b => b.Payee)
            .Where(b => b.UserId == userId)
            .ToListAsync();

    private static List<BudgetItemViewModel> ProjectForecast(
        decimal startingBalance,
        IEnumerable<Budget> budgets,
        DateTime startDate,
        DateTime endDate,
        bool includeEndDate)
    {
        var projectedItems = new List<(int BudgetId, string BudgetName, string Category, DateTime DueDate, decimal Amount, int FrequencyId, string FrequencyName, bool IsAuto, bool IsBill, bool IsLate, string Payee)>();

        foreach (var budget in budgets)
        {
            var nextDue = budget.NextDueDate ?? startDate;
            var budgetEndDate = HasEndDate(budget) ? budget.EndDate!.Value : endDate;
            var frequencyId = budget.FrequencyId ?? 0;

            while (IsWithinProjection(nextDue, endDate, includeEndDate) && IsWithinProjection(nextDue, budgetEndDate, includeEndDate))
            {
                var amount = budget.BudgetTypeId == 0 ? (budget.Amount ?? 0m) : -(budget.Amount ?? 0m);
                projectedItems.Add((
                    budget.BudgetId,
                    budget.BudgetName ?? string.Empty,
                    budget.Category?.CategoryName ?? string.Empty,
                    nextDue,
                    amount,
                    frequencyId,
                    budget.Frequency?.FrequencyName ?? string.Empty,
                    budget.IsAutomatic ?? false,
                    budget.IsBill ?? false,
                    budget.IsLate ?? false,
                    budget.Payee?.PayeeName ?? string.Empty));

                if (frequencyId == 0)
                    break;

                nextDue = CalculateNextDueDate(nextDue, frequencyId);
            }
        }

        var runningBalance = startingBalance;
        var result = new List<BudgetItemViewModel>();
        foreach (var item in projectedItems.OrderBy(i => i.DueDate).ThenByDescending(i => i.Amount))
        {
            runningBalance += item.Amount;
            result.Add(new BudgetItemViewModel
            {
                BudgetId = item.BudgetId,
                BudgetName = item.BudgetName,
                Category = item.Category,
                DueDate = item.DueDate,
                Amount = item.Amount,
                Balance = runningBalance,
                FrequencyName = item.FrequencyName,
                IsAuto = item.IsAuto,
                IsBill = item.IsBill,
                IsLate = item.IsLate,
                Payee = item.Payee
            });
        }

        return result;
    }

    private static bool ShouldDeleteAfterAdvance(Budget budget, DateTime newNextDueDate)
    {
        if ((budget.FrequencyId ?? 0) == 0)
            return true;

        return HasEndDate(budget) && newNextDueDate > budget.EndDate;
    }

    private static bool HasEndDate(Budget budget) =>
        budget.EndDate.HasValue && budget.EndDate.Value != NoEndDate;

    private static bool IsWithinProjection(DateTime value, DateTime endDate, bool includeEndDate) =>
        includeEndDate ? value <= endDate : value < endDate;

    private static DateTime CalculateSemiMonthly(DateTime currentDate) =>
        currentDate.Day == 1
            ? currentDate.AddDays(14)
            : new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1);

    private async Task<int> GetOrCreateCategoryAsync(string categoryName, int userId)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.UserId == userId);
        if (category != null)
            return category.CategoryId;

        var newCategory = new Category { CategoryName = categoryName, UserId = userId };
        _db.Categories.Add(newCategory);
        await _db.SaveChangesAsync();
        return newCategory.CategoryId;
    }

    private async Task<int> GetOrCreatePayeeAsync(string payeeName, int userId)
    {
        if (string.IsNullOrWhiteSpace(payeeName))
            return -1;

        var payee = await _db.Payees.FirstOrDefaultAsync(p => p.PayeeName == payeeName && p.UserId == userId);
        if (payee != null)
            return payee.PayeeId;

        var newPayee = new Payee { PayeeName = payeeName, UserId = userId, IsDeleted = false };
        _db.Payees.Add(newPayee);
        await _db.SaveChangesAsync();
        return newPayee.PayeeId;
    }
}
