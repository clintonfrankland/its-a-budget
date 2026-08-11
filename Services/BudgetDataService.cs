using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class BudgetDataService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public BudgetDataService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public async Task<Account?> GetAccountForUserAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Accounts
            .AsNoTracking()
            .Where(a => a.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(a.SharedBudgetId.Value)
                : a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.AccountId)
            .FirstOrDefaultAsync();
    }

    public async Task<decimal> GetTransactionSumAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.ReadableTransactions(userId, sharedBudgetIds)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
    }

    public async Task<List<Budget>> GetBudgetsForUserAsync(int userId)
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

    public async Task<List<Category>> GetCategoriesForUserAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(c.SharedBudgetId.Value)
                : c.UserId == userId)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }
    public Task<List<Payee>> GetPayeesForUserAsync(int userId) => _db.Payees.AsNoTracking().Where(p => p.UserId == userId && !p.IsDeleted).OrderBy(p => p.PayeeName).ToListAsync();
    public Task<List<Frequency>> GetFrequenciesAsync() => _db.Frequencies.AsNoTracking().OrderBy(f => f.Sort).ToListAsync();

    public async Task<Budget?> GetBudgetByIdAsync(int userId, int budgetId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Payee)
            .FirstOrDefaultAsync(b =>
                b.BudgetId == budgetId &&
                (b.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(b.SharedBudgetId.Value)
                    : b.UserId == userId));
    }

    public async Task SaveBudgetAsync(
        int userId,
        int budgetId,
        string budgetName,
        int budgetTypeId,
        int frequencyId,
        DateTime nextDueDate,
        DateTime endDate,
        decimal amount,
        string categoryName,
        string payeeName,
        bool isAutomatic,
        bool isBill,
        bool isLate,
        bool isSpendingAllowance = false)
    {
        ValidateBudgetSave(nextDueDate, endDate, amount, frequencyId, isSpendingAllowance);
        var isNew = budgetId == -1;
        var budget = isNew
            ? new Budget { UserId = userId, SharedBudgetId = await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId) }
            : await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId);

        if (budget is null)
            return;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, budget.SharedBudgetId, budget.UserId))
            return;

        var roundedAmount = CurrencyPolicy.RoundNonNegativeSqlAmount(amount);
        var categoryId = await GetOrCreateCategoryAsync(categoryName, userId, budget.SharedBudgetId);
        if (categoryId <= 0)
            throw new InvalidOperationException("Category is required.");

        if (isSpendingAllowance)
        {
            var candidate = new Budget
            {
                IsSpendingAllowance = true,
                FrequencyId = frequencyId,
                NextDueDate = nextDueDate,
                EndDate = endDate
            };
            var sameCategoryAllowances = await _db.Budgets.AsNoTracking()
                .Where(b => b.BudgetId != budgetId && b.IsSpendingAllowance && b.CategoryId == categoryId &&
                    (b.SharedBudgetId.HasValue ? b.SharedBudgetId == budget.SharedBudgetId : !b.SharedBudgetId.HasValue && b.UserId == userId))
                .ToListAsync();
            if (sameCategoryAllowances.Any(existing => BudgetAllowanceService.Overlaps(existing, candidate)))
                throw new InvalidOperationException("That category already has an overlapping spending allowance.");
        }

        var payeeId = await GetOrCreatePayeeAsync(payeeName, userId);

        budget.BudgetName = budgetName;
        budget.BudgetTypeId = isSpendingAllowance ? 1 : budgetTypeId;
        budget.FrequencyId = frequencyId;
        budget.NextDueDate = nextDueDate;
        budget.EndDate = endDate;
        budget.Amount = roundedAmount;
        budget.CategoryId = categoryId;
        budget.UserId = userId;
        budget.IsSpendingAllowance = isSpendingAllowance;
        budget.IsAutomatic = !isSpendingAllowance && isAutomatic;
        budget.IsBill = !isSpendingAllowance && isBill;
        budget.IsLate = !isSpendingAllowance && isLate;
        budget.PayeeId = !isSpendingAllowance && payeeId > 0 ? payeeId : null;

        if (isNew) _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
    }

    private static void ValidateBudgetSave(DateTime nextDueDate, DateTime endDate, decimal amount, int frequencyId, bool isSpendingAllowance)
    {
        if (nextDueDate == DateTime.MinValue)
            throw new InvalidOperationException("Next due date is required.");

        if (endDate == DateTime.MinValue)
            throw new InvalidOperationException("End date is required.");

        if (endDate != new DateTime(1970, 1, 1) && endDate < nextDueDate)
            throw new InvalidOperationException("End date cannot be before the next due date.");

        CurrencyPolicy.RoundNonNegativeSqlAmount(amount);

        if (isSpendingAllowance && frequencyId == 0)
            throw new InvalidOperationException("A spending allowance must use a recurring frequency.");

        if (isSpendingAllowance && frequencyId == 8 && nextDueDate.Day is not (1 or 15))
            throw new InvalidOperationException("A semi-monthly spending allowance must reset on the 1st or 15th.");
    }

    public async Task DeleteBudgetAsync(int userId, int budgetId)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId);
        if (budget is null) return;
        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, budget.SharedBudgetId, budget.UserId))
            return;

        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync();
    }

    public Task<int> GetOrCreateCategoryAsync(string categoryName, int userId) =>
        GetOrCreateCategoryAsync(categoryName, userId, null);

    private async Task<int> GetOrCreateCategoryAsync(string categoryName, int userId, int? sharedBudgetId)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return -1;
        var category = sharedBudgetId.HasValue
            ? await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.SharedBudgetId == sharedBudgetId)
            : await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.UserId == userId);
        if (category != null) return category.CategoryId;
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

    public async Task<int> GetOrCreatePayeeAsync(string payeeName, int userId)
    {
        if (string.IsNullOrWhiteSpace(payeeName)) return -1;
        var payee = await _db.Payees.FirstOrDefaultAsync(p => p.PayeeName == payeeName && p.UserId == userId);
        if (payee != null) return payee.PayeeId;
        var newPayee = new Payee { PayeeName = payeeName, UserId = userId, IsDeleted = false };
        _db.Payees.Add(newPayee);
        await _db.SaveChangesAsync();
        return newPayee.PayeeId;
    }
}
