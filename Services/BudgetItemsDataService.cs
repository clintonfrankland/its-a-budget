using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class BudgetItemsDataService
{
    private readonly ClintonFranklandDbContext _db;
    public BudgetItemsDataService(ClintonFranklandDbContext db) => _db = db;

    public Task<List<Budget>> GetBudgetsForUserAsync(int userId) =>
        _db.Budgets.Include(b => b.Category).Include(b => b.Frequency).Where(b => b.UserId == userId).ToListAsync();

    public Task<List<Category>> GetCategoriesForUserAsync(int userId) =>
        _db.Categories.Where(c => c.UserId == userId).OrderBy(c => c.CategoryName).ToListAsync();

    public Task<List<Payee>> GetPayeesForUserAsync(int userId) =>
        _db.Payees.Where(p => p.UserId == userId && !p.IsDeleted).OrderBy(p => p.PayeeName).ToListAsync();

    public Task<List<Frequency>> GetFrequenciesAsync() =>
        _db.Frequencies.OrderBy(f => f.Sort).ToListAsync();

    public Task<Budget?> GetBudgetByIdAsync(int budgetId) =>
        _db.Budgets.Include(b => b.Category).Include(b => b.Payee).FirstOrDefaultAsync(b => b.BudgetId == budgetId);

    public Task<Budget?> FindBudgetAsync(int budgetId) => _db.Budgets.FindAsync(budgetId).AsTask();

    public async Task SaveBudgetAsync(Budget budget, bool isNew)
    {
        budget.Amount = CurrencyPolicy.Round(budget.Amount);
        if (isNew) _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetOrCreateCategoryAsync(string categoryName, int userId)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return -1;
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.UserId == userId);
        if (category != null) return category.CategoryId;
        var newCategory = new Category { CategoryName = categoryName, UserId = userId };
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

    public async Task DeleteBudgetAsync(int budgetId)
    {
        var budget = await _db.Budgets.FindAsync(budgetId);
        if (budget is null) return;
        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync();
    }
}
