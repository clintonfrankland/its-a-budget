using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class CheckbookDataService
{
    private readonly ClintonFranklandDbContext _db;
    public CheckbookDataService(ClintonFranklandDbContext db) => _db = db;

    public Task<Account?> GetAccountForUserAsync(int userId) => _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);

    public Task<List<Transaction>> GetTransactionsForUserAsync(int userId) =>
        _db.Transactions.Include(t => t.Payee).Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Cleared).ThenBy(t => t.TransactionDate).ThenByDescending(t => t.Amount)
            .ToListAsync();

    public Task<List<Payee>> GetPayeesForUserAsync(int userId) => _db.Payees.Where(p => p.UserId == userId && !p.IsDeleted).OrderBy(p => p.PayeeName).ToListAsync();
    public Task<List<Category>> GetCategoriesForUserAsync(int userId) => _db.Categories.Where(c => c.UserId == userId).OrderBy(c => c.CategoryName).ToListAsync();
    public Task<decimal> GetTransactionSumAsync(int userId) => _db.Transactions.Where(t => t.UserId == userId).SumAsync(t => (decimal?)t.Amount).ContinueWith(t => t.Result ?? 0m);
    public Task<List<Budget>> GetBudgetsForUserAsync(int userId) => _db.Budgets.Include(b => b.Category).Include(b => b.Frequency).Include(b => b.Payee).Where(b => b.UserId == userId).ToListAsync();

    public Task<Transaction?> GetTransactionByIdAsync(int id) => _db.Transactions.Include(t => t.Payee).Include(t => t.Category).FirstOrDefaultAsync(t => t.TransactionId == id);
    public Task<Transaction?> FindTransactionAsync(int id) => _db.Transactions.FindAsync(id).AsTask();
    public Task<Budget?> FindBudgetAsync(int id) => _db.Budgets.FindAsync(id).AsTask();

    public async Task SaveTransactionAsync(Transaction txn, bool isNew)
    {
        if (isNew) _db.Transactions.Add(txn);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTransactionAsync(int id)
    {
        var txn = await _db.Transactions.FindAsync(id);
        if (txn is null) return;
        _db.Transactions.Remove(txn);
        await _db.SaveChangesAsync();
    }

    public async Task SaveBudgetAsync(Budget budget)
    {
        await _db.SaveChangesAsync();
    }

    public async Task DeleteBudgetAsync(Budget budget)
    {
        _db.Budgets.Remove(budget);
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
}
