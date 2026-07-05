using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class BudgetItemsDataService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public BudgetItemsDataService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public Task<List<Budget>> GetBudgetsForUserAsync(int userId) =>
        _db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Frequency)
            .Include(b => b.Payee)
            .Where(b => b.UserId == userId)
            .ToListAsync();

    public Task<List<Category>> GetCategoriesForUserAsync(int userId) =>
        _db.Categories.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.CategoryName).ToListAsync();

    public Task<List<Payee>> GetPayeesForUserAsync(int userId) =>
        _db.Payees.AsNoTracking().Where(p => p.UserId == userId && !p.IsDeleted).OrderBy(p => p.PayeeName).ToListAsync();

    public Task<List<Frequency>> GetFrequenciesAsync() =>
        _db.Frequencies.AsNoTracking().OrderBy(f => f.Sort).ToListAsync();

    public Task<Budget?> GetBudgetByIdAsync(int userId, int budgetId) =>
        _db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Payee)
            .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);

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
        bool isLate)
    {
        ValidateBudgetSave(nextDueDate, endDate, amount);
        var roundedAmount = CurrencyPolicy.RoundNonNegativeSqlAmount(amount);
        var categoryId = await GetOrCreateCategoryAsync(categoryName, userId);
        if (categoryId <= 0)
            throw new InvalidOperationException("Category is required.");

        var payeeId = await GetOrCreatePayeeAsync(payeeName, userId);
        var isNew = budgetId == -1;
        var budget = isNew
            ? new Budget { UserId = userId, SharedBudgetId = await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId) }
            : await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);

        if (budget is null)
            return;

        budget.BudgetName = budgetName;
        budget.BudgetTypeId = budgetTypeId;
        budget.FrequencyId = frequencyId;
        budget.NextDueDate = nextDueDate;
        budget.EndDate = endDate;
        budget.Amount = roundedAmount;
        budget.CategoryId = categoryId;
        budget.UserId = userId;
        budget.IsAutomatic = isAutomatic;
        budget.IsBill = isBill;
        budget.IsLate = isLate;
        budget.PayeeId = payeeId > 0 ? payeeId : null;

        if (isNew) _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
    }

    private static void ValidateBudgetSave(DateTime nextDueDate, DateTime endDate, decimal amount)
    {
        if (nextDueDate == DateTime.MinValue)
            throw new InvalidOperationException("Next due date is required.");

        if (endDate == DateTime.MinValue)
            throw new InvalidOperationException("End date is required.");

        if (endDate != new DateTime(1970, 1, 1) && endDate < nextDueDate)
            throw new InvalidOperationException("End date cannot be before the next due date.");

        CurrencyPolicy.RoundNonNegativeSqlAmount(amount);
    }

    public async Task<int> GetOrCreateCategoryAsync(string categoryName, int userId)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return -1;
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.UserId == userId);
        if (category != null) return category.CategoryId;
        var newCategory = new Category
        {
            CategoryName = categoryName,
            UserId = userId,
            SharedBudgetId = await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId)
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

    public async Task DeleteBudgetAsync(int userId, int budgetId)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);
        if (budget is null) return;
        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync();
    }
}
