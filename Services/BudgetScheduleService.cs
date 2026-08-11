using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class BudgetScheduleService
{
    private static readonly DateTime NoEndDate = new(1970, 1, 1);

    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public BudgetScheduleService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public async Task<List<BudgetItemViewModel>> GetForecastAsync(int userId, DateTime endDate, bool includeEndDate = false)
        => await GetForecastAsync(userId, DateTime.Today, endDate, includeEndDate);

    public async Task<List<BudgetItemViewModel>> GetForecastAsync(
        int userId,
        DateTime startDate,
        DateTime endDate,
        bool includeEndDate = false)
    {
        // The projection starts with the cleared ledger balance and treats every
        // uncleared transaction as already committed today, regardless of the
        // transaction date entered in Checkbook.
        var startingBalance = await GetProjectionOpeningBalanceAsync(userId);
        var budgets = await GetUserBudgetsWithLookupsAsync(userId);
        var writableSharedBudgetIds = await _sharedBudgets.GetFinancialManagerSharedBudgetIdsAsync(userId);

        var incomeBudget = budgets
            .Where(b => b.BudgetTypeId == 0)
            .OrderBy(b => b.NextDueDate)
            .FirstOrDefault();

        if (incomeBudget?.NextDueDate > endDate)
            endDate = incomeBudget.NextDueDate.Value;

        var allowances = await BuildAllowanceForecastAsync(userId, budgets, startDate, endDate, includeEndDate);
        return ProjectForecast(startingBalance, budgets, startDate, endDate, includeEndDate, userId, writableSharedBudgetIds, allowances);
    }

    public async Task<List<BudgetItemViewModel>> GetRecurringPreviewAsync(
        int userId,
        DateTime startDate,
        int days)
    {
        if (days < 1)
            throw new InvalidOperationException("Preview window must be at least 1 day.");

        var budgets = await GetUserBudgetsWithLookupsAsync(userId);
        var writableSharedBudgetIds = await _sharedBudgets.GetFinancialManagerSharedBudgetIdsAsync(userId);
        var endDate = startDate.Date.AddDays(days - 1);
        var allowances = await BuildAllowanceForecastAsync(userId, budgets, startDate.Date, endDate, includeEndDate: true);
        return ProjectForecast(
            startingBalance: 0m,
            budgets,
            startDate.Date,
            endDate,
            includeEndDate: true,
            userId,
            writableSharedBudgetIds,
            allowances,
            rollForwardToStart: true);
    }

    public async Task<(decimal LowestBalance, DateTime LowestDate)> GetLowestProjectedBalanceAsync(
        int userId,
        DateTime startDate,
        DateTime endDate)
    {
        var currentBalance = await GetProjectionOpeningBalanceAsync(userId);
        var budgets = await GetUserBudgetsWithLookupsAsync(userId);
        var writableSharedBudgetIds = await _sharedBudgets.GetFinancialManagerSharedBudgetIdsAsync(userId);
        var allowances = await BuildAllowanceForecastAsync(userId, budgets, startDate, endDate, includeEndDate: true);
        var forecast = ProjectForecast(currentBalance, budgets, startDate, endDate, includeEndDate: true, userId, writableSharedBudgetIds, allowances);

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
        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId);
        if (budget is null)
            return;

        if (budget.IsSpendingAllowance)
            return;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, budget.SharedBudgetId, budget.UserId))
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

    public async Task<bool> SkipOccurrenceAsync(int userId, int budgetId, DateTime occurrenceDate)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId);
        if (budget is null)
            return false;

        if (budget.IsSpendingAllowance)
            return false;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, budget.SharedBudgetId, budget.UserId))
            return false;

        if (!CanHandleOccurrence(budget, occurrenceDate))
            return false;

        AdvanceBudgetThroughOccurrence(budget, occurrenceDate);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RecordOccurrenceToCheckbookAsync(int userId, int budgetId, DateTime occurrenceDate)
    {
        var budget = await _db.Budgets
            .Include(b => b.Category)
            .Include(b => b.Payee)
            .FirstOrDefaultAsync(b => b.BudgetId == budgetId);

        if (budget is null)
            return false;

        if (budget.IsSpendingAllowance)
            return false;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, budget.SharedBudgetId, budget.UserId))
            return false;

        if (!CanHandleOccurrence(budget, occurrenceDate))
            return false;

        var categoryWarning = GetCategoryWarning(budget);
        if (!string.IsNullOrEmpty(categoryWarning))
            throw new InvalidOperationException(categoryWarning);

        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == budget.CategoryId);
        categoryWarning = GetCategoryWarning(budget, category);
        if (!string.IsNullOrEmpty(categoryWarning))
            throw new InvalidOperationException(categoryWarning);

        var account = budget.SharedBudgetId.HasValue
            ? await _db.Accounts
                .AsNoTracking()
                .Where(a => a.SharedBudgetId == budget.SharedBudgetId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.AccountId)
                .FirstOrDefaultAsync()
            : await _db.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.AccountId)
                .FirstOrDefaultAsync();

        var payeeId = budget.PayeeId;
        if (!payeeId.HasValue || payeeId <= 0)
            payeeId = await GetOrCreatePayeeAsync(budget.BudgetName ?? string.Empty, userId);

        if (!payeeId.HasValue || payeeId <= 0)
            payeeId = await GetOrCreatePayeeAsync("Unknown", userId);

        var amount = budget.BudgetTypeId == 0 ? (budget.Amount ?? 0m) : -(budget.Amount ?? 0m);
        _db.Transactions.Add(new Transaction
        {
            TransactionDate = DateOnly.FromDateTime(occurrenceDate.Date),
            Amount = CurrencyPolicy.RoundSignedSqlAmount(amount, CurrencyPolicy.TransactionPrecision),
            PayeeId = payeeId.Value,
            CategoryId = budget.CategoryId,
            AccountId = account?.AccountId ?? 1,
            Cleared = budget.IsAutomatic ?? false,
            UserId = budget.SharedBudgetId.HasValue ? userId : budget.UserId ?? userId,
            SharedBudgetId = budget.SharedBudgetId,
            Notes = $"Recorded from Budget Item: {budget.BudgetName}".Trim()
        });

        AdvanceBudgetThroughOccurrence(budget, occurrenceDate);
        await _db.SaveChangesAsync();
        return true;
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
            .FirstOrDefaultAsync(b => b.BudgetId == originalBudgetId);

        if (originalBudget is null)
            return;

        if (originalBudget.IsSpendingAllowance)
            return;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, originalBudget.SharedBudgetId, originalBudget.UserId))
            return;

        var categoryId = await GetOrCreateCategoryAsync(categoryName.Trim(), userId, originalBudget.SharedBudgetId);
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
            SharedBudgetId = originalBudget.SharedBudgetId ?? await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId),
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

    private async Task<decimal> GetProjectionOpeningBalanceAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var account = await _db.Accounts
            .AsNoTracking()
            .Where(a => !(a.IsDeleted ?? false) && (a.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(a.SharedBudgetId.Value)
                : a.UserId == userId))
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.AccountId)
            .FirstOrDefaultAsync();
        var transactions = _db.ReadableTransactions(userId, sharedBudgetIds);

        var clearedBalance = (account?.BeginningBalance ?? 0m)
            + (await transactions
                .Where(t => t.Cleared)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m);
        var committedUncleared = await transactions
            .Where(t => !t.Cleared)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return CurrencyPolicy.Round(clearedBalance + committedUncleared);
    }

    private async Task<List<Budget>> GetUserBudgetsWithLookupsAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Frequency)
            .Include(b => b.Payee)
            .Where(b => b.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(b.SharedBudgetId.Value)
                : b.UserId == userId)
            .ToListAsync();
    }

    private static List<BudgetItemViewModel> ProjectForecast(
        decimal startingBalance,
        IEnumerable<Budget> budgets,
        DateTime startDate,
        DateTime endDate,
        bool includeEndDate,
        int userId,
        IReadOnlyCollection<int> writableSharedBudgetIds,
        IReadOnlyDictionary<string, AllowanceForecastValue> allowanceForecast,
        bool rollForwardToStart = false)
    {
        var projectedItems = new List<(int BudgetId, string BudgetName, string Category, DateTime DueDate, decimal Amount, int FrequencyId, string FrequencyName, bool IsAuto, bool IsBill, bool IsLate, string Payee, bool CanManageFinancialData, string CategoryWarning)>();
        var categoriesById = budgets
            .Select(b => b.Category)
            .OfType<Category>()
            .GroupBy(c => c.CategoryId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var budget in budgets)
        {
            var nextDue = budget.IsSpendingAllowance
                ? BudgetAllowanceService.GetCurrentPeriod(budget, DateOnly.FromDateTime(startDate)).End.ToDateTime(TimeOnly.MinValue)
                : budget.NextDueDate ?? startDate;
            var budgetEndDate = HasEndDate(budget) ? budget.EndDate!.Value : endDate;
            var frequencyId = budget.FrequencyId ?? 0;
            if (rollForwardToStart)
            {
                nextDue = RollForwardToProjectionStart(nextDue, frequencyId, startDate);
                if (nextDue.Date < startDate.Date)
                    continue;
            }

            while (IsWithinProjection(nextDue, endDate, includeEndDate) && IsWithinProjection(nextDue, budgetEndDate, includeEndDate))
            {
                var allowanceKey = AllowanceKey(budget.BudgetId, nextDue);
                allowanceForecast.TryGetValue(allowanceKey, out var allowance);
                var amount = budget.IsSpendingAllowance
                    ? -(allowance?.RemainingAmount ?? CurrencyPolicy.Round(budget.Amount ?? 0m))
                    : budget.BudgetTypeId == 0 ? (budget.Amount ?? 0m) : -(budget.Amount ?? 0m);
                projectedItems.Add((
                    budget.BudgetId,
                    budget.IsSpendingAllowance ? $"{budget.BudgetName} allowance".Trim() : budget.BudgetName ?? string.Empty,
                    budget.Category?.CategoryName ?? string.Empty,
                    nextDue,
                    amount,
                    frequencyId,
                    budget.Frequency?.FrequencyName ?? string.Empty,
                    budget.IsAutomatic ?? false,
                    budget.IsBill ?? false,
                    budget.IsLate ?? false,
                    budget.Payee?.PayeeName ?? string.Empty,
                    CanManageFinancialData(userId, writableSharedBudgetIds, budget.SharedBudgetId, budget.UserId),
                    GetCategoryWarning(budget, categoriesById.GetValueOrDefault(budget.CategoryId))));

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
                IsSpendingAllowance = allowanceForecast.TryGetValue(AllowanceKey(item.BudgetId, item.DueDate), out var allowance),
                PlannedAmount = allowance?.PlannedAmount ?? 0m,
                SpentAmount = allowance?.SpentAmount ?? 0m,
                RemainingAmount = allowance?.RemainingAmount ?? 0m,
                Payee = item.Payee,
                CanManageFinancialData = item.CanManageFinancialData,
                HasCategoryWarning = !string.IsNullOrEmpty(item.CategoryWarning),
                CategoryWarning = item.CategoryWarning
            });
        }

        return result;
    }

    private async Task<Dictionary<string, AllowanceForecastValue>> BuildAllowanceForecastAsync(
        int userId,
        IReadOnlyCollection<Budget> budgets,
        DateTime startDate,
        DateTime endDate,
        bool includeEndDate)
    {
        var projectionStart = DateOnly.FromDateTime(startDate);
        var allowances = budgets
            .Where(b => b.IsSpendingAllowance &&
                (!HasEndDate(b) || DateOnly.FromDateTime(b.EndDate!.Value) >= projectionStart))
            .ToList();
        if (allowances.Count == 0)
            return [];

        var readable = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var start = DateOnly.FromDateTime(startDate);
        var earliest = allowances.Min(b => BudgetAllowanceService.GetCurrentPeriod(b, start).Start);
        var actuals = await _db.ReadableTransactions(userId, readable)
            .Where(t => t.Amount < 0 && t.TransactionDate >= earliest && t.TransactionDate <= start)
            .Select(t => new { t.CategoryId, t.TransactionDate, t.Amount, t.SharedBudgetId, t.UserId })
            .ToListAsync();

        var result = new Dictionary<string, AllowanceForecastValue>();
        foreach (var budget in allowances)
        {
            var period = BudgetAllowanceService.GetCurrentPeriod(budget, start);
            var periodStart = period.Start;
            var periodEnd = period.End;
            while (IsWithinProjection(periodEnd.ToDateTime(TimeOnly.MinValue), endDate, includeEndDate) &&
                   (!HasEndDate(budget) || periodEnd <= DateOnly.FromDateTime(budget.EndDate!.Value)))
            {
                var spent = CurrencyPolicy.Round(actuals
                    .Where(t => t.CategoryId == budget.CategoryId && t.TransactionDate >= periodStart && t.TransactionDate < periodEnd &&
                        SameBudgetScope(budget, t.SharedBudgetId, t.UserId))
                    .Sum(t => -t.Amount));
                var planned = CurrencyPolicy.Round(budget.Amount ?? 0m);
                var remaining = CurrencyPolicy.Round(Math.Max(0m, planned - spent));
                result[AllowanceKey(budget.BudgetId, periodEnd.ToDateTime(TimeOnly.MinValue))] =
                    new AllowanceForecastValue(planned, spent, remaining);

                periodStart = periodEnd;
                var next = DateOnly.FromDateTime(CalculateNextDueDate(periodEnd.ToDateTime(TimeOnly.MinValue), budget.FrequencyId ?? 0));
                if (next <= periodEnd)
                    break;
                periodEnd = next;
            }
        }

        return result;
    }

    private static string AllowanceKey(int budgetId, DateTime periodEnd) => $"{budgetId}:{periodEnd:yyyyMMdd}";

    private static bool SameBudgetScope(Budget budget, int? sharedBudgetId, int? ownerUserId) =>
        budget.SharedBudgetId.HasValue
            ? sharedBudgetId == budget.SharedBudgetId
            : !sharedBudgetId.HasValue && ownerUserId == budget.UserId;

    private sealed record AllowanceForecastValue(decimal PlannedAmount, decimal SpentAmount, decimal RemainingAmount);

    private static bool CanManageFinancialData(
        int userId,
        IReadOnlyCollection<int> writableSharedBudgetIds,
        int? sharedBudgetId,
        int? ownerUserId) =>
        sharedBudgetId.HasValue
            ? writableSharedBudgetIds.Contains(sharedBudgetId.Value)
            : ownerUserId == userId;

    private static bool ShouldDeleteAfterAdvance(Budget budget, DateTime newNextDueDate)
    {
        if ((budget.FrequencyId ?? 0) == 0)
            return true;

        return HasEndDate(budget) && newNextDueDate > budget.EndDate;
    }

    private static bool HasEndDate(Budget budget) =>
        budget.EndDate.HasValue && budget.EndDate.Value != NoEndDate;

    private static bool CanHandleOccurrence(Budget budget, DateTime occurrenceDate)
    {
        var currentDueDate = budget.NextDueDate ?? DateTime.Today;
        if (occurrenceDate.Date < currentDueDate.Date)
            return false;

        if (HasEndDate(budget) && occurrenceDate.Date > budget.EndDate!.Value.Date)
            return false;

        var frequencyId = budget.FrequencyId ?? 0;
        while (currentDueDate.Date < occurrenceDate.Date)
        {
            if (frequencyId == 0)
                return false;

            var nextDueDate = CalculateNextDueDate(currentDueDate, frequencyId);
            if (nextDueDate.Date <= currentDueDate.Date)
                return false;

            currentDueDate = nextDueDate;
        }

        return currentDueDate.Date == occurrenceDate.Date;
    }

    private void AdvanceBudgetOccurrence(Budget budget)
    {
        var newNextDueDate = CalculateNextDueDate(budget.NextDueDate ?? DateTime.Today, budget.FrequencyId ?? 0);

        if (ShouldDeleteAfterAdvance(budget, newNextDueDate))
        {
            _db.Budgets.Remove(budget);
        }
        else
        {
            budget.NextDueDate = newNextDueDate;
        }
    }

    private void AdvanceBudgetThroughOccurrence(Budget budget, DateTime occurrenceDate)
    {
        while (budget.NextDueDate.HasValue && budget.NextDueDate.Value.Date <= occurrenceDate.Date && _db.Entry(budget).State != EntityState.Deleted)
        {
            AdvanceBudgetOccurrence(budget);
        }
    }

    private static string GetCategoryWarning(Budget budget) =>
        GetCategoryWarning(budget, budget.Category);

    private static string GetCategoryWarning(Budget budget, Category? category)
    {
        if (category is null)
            return "Category is missing. Edit the Budget Item and choose a valid category before recording.";

        if (string.IsNullOrWhiteSpace(category.CategoryName))
            return "Category is blank. Edit the Budget Item and choose a valid category before recording.";

        if (budget.SharedBudgetId.HasValue)
        {
            if (category.SharedBudgetId != budget.SharedBudgetId)
                return "Category is outside this budget. Edit the Budget Item and choose a category from the same budget before recording.";
        }
        else if (category.UserId != budget.UserId)
        {
            return "Category belongs to another user. Edit the Budget Item and choose one of your categories before recording.";
        }

        return string.Empty;
    }

    private static bool IsWithinProjection(DateTime value, DateTime endDate, bool includeEndDate) =>
        includeEndDate ? value <= endDate : value < endDate;

    private static DateTime RollForwardToProjectionStart(DateTime dueDate, int frequencyId, DateTime startDate)
    {
        while (dueDate.Date < startDate.Date)
        {
            if (frequencyId == 0)
                return dueDate;

            var nextDueDate = CalculateNextDueDate(dueDate, frequencyId);
            if (nextDueDate.Date <= dueDate.Date)
                return dueDate;

            dueDate = nextDueDate;
        }

        return dueDate;
    }

    private static DateTime CalculateSemiMonthly(DateTime currentDate) =>
        currentDate.Day == 1
            ? currentDate.AddDays(14)
            : new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1);

    private async Task<int> GetOrCreateCategoryAsync(string categoryName, int userId, int? sharedBudgetId = null)
    {
        var category = sharedBudgetId.HasValue
            ? await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.SharedBudgetId == sharedBudgetId)
            : await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.UserId == userId);
        if (category != null)
            return category.CategoryId;

        var newCategory = new Category
        {
            CategoryName = categoryName,
            UserId = userId,
            SharedBudgetId = sharedBudgetId ?? await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId)
        };
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
