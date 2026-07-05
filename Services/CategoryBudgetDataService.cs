using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Services;

public class CategoryBudgetDataService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;
    private readonly CategoryBudgetThresholds _thresholds;

    public CategoryBudgetDataService(
        ClintonFranklandDbContext db,
        SharedBudgetDataService sharedBudgets,
        IOptions<CategoryBudgetAlertOptions> options)
    {
        _db = db;
        _sharedBudgets = sharedBudgets;
        _thresholds = CategoryBudgetThresholds.Validate(options.Value.WarningPercent);
    }

    public CategoryBudgetDataService(
        ClintonFranklandDbContext db,
        IOptions<CategoryBudgetAlertOptions> options)
        : this(db, new SharedBudgetDataService(db), options)
    {
    }

    public CategoryBudgetThresholds Thresholds => _thresholds;

    public async Task<List<CategoryBudgetCategoryOption>> GetCategoryOptionsAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.SharedBudgetId.HasValue
                ? sharedBudgetIds.Contains(c.SharedBudgetId.Value)
                : c.UserId == userId)
            .OrderBy(c => c.CategoryName)
            .Select(c => new { c.CategoryId, c.CategoryName })
            .ToListAsync();

        return categories
            .Select(c => new CategoryBudgetCategoryOption(
                c.CategoryId,
                string.IsNullOrWhiteSpace(c.CategoryName) ? InsightsDataService.UncategorizedCategoryName : c.CategoryName.Trim()))
            .ToList();
    }

    public async Task<List<CategoryBudgetMonthRow>> GetMonthRowsAsync(int userId, DateOnly selectedMonth)
    {
        var monthStart = FirstOfMonth(selectedMonth);
        var monthEnd = monthStart.AddMonths(1);
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);

        var targets = await _db.CategoryBudgetTargets
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t =>
                (t.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(t.SharedBudgetId.Value)
                    : t.UserId == userId) &&
                t.Category != null &&
                (t.Category.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(t.Category.SharedBudgetId.Value)
                    : t.Category.UserId == userId) &&
                t.BudgetMonth == monthStart)
            .ToListAsync();

        var spending = await _db.Transactions
            .AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t =>
                (t.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(t.SharedBudgetId.Value)
                    : t.UserId == userId) &&
                t.Account != null &&
                (t.Account.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(t.Account.SharedBudgetId.Value)
                    : t.Account.UserId == userId) &&
                t.Category != null &&
                (t.Category.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(t.Category.SharedBudgetId.Value)
                    : t.Category.UserId == userId) &&
                t.Amount < 0 &&
                t.TransactionDate >= monthStart &&
                t.TransactionDate < monthEnd)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, ActualAmount = g.Sum(t => -t.Amount) })
            .ToListAsync();

        var spendingByCategory = spending.ToDictionary(x => x.CategoryId, x => x.ActualAmount);
        var targetCategoryIds = targets.Select(t => t.CategoryId).ToHashSet();

        var spentOnlyCategories = await _db.Categories
            .AsNoTracking()
            .Where(c =>
                (c.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(c.SharedBudgetId.Value)
                    : c.UserId == userId) &&
                spendingByCategory.Keys.Contains(c.CategoryId) &&
                !targetCategoryIds.Contains(c.CategoryId))
            .ToListAsync();

        var budgetedRows = targets.Select(t => CreateRow(
            t.CategoryBudgetTargetId,
            t.CategoryId,
            GetCategoryName(t.Category),
            t.PlannedAmount,
            spendingByCategory.GetValueOrDefault(t.CategoryId)));

        var spentOnlyRows = spentOnlyCategories.Select(c => CreateRow(
            null,
            c.CategoryId,
            GetCategoryName(c),
            0m,
            spendingByCategory.GetValueOrDefault(c.CategoryId)));

        return budgetedRows
            .Concat(spentOnlyRows)
            .OrderByDescending(r => r.AlertStatus)
            .ThenBy(r => r.CategoryName)
            .ToList();
    }

    public async Task SaveTargetAsync(int userId, CategoryBudgetSaveRequest request)
    {
        var monthStart = FirstOfMonth(request.BudgetMonth);
        var plannedAmount = CurrencyPolicy.RoundNonNegativeSqlAmount(request.PlannedAmount);

        var category = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null || !await _sharedBudgets.CanManageFinancialDataAsync(userId, category.SharedBudgetId, category.UserId))
            throw new InvalidOperationException("Category was not found for the current user.");

        CategoryBudgetTarget? target = null;
        if (request.TargetId.HasValue)
        {
            target = await _db.CategoryBudgetTargets
                .FirstOrDefaultAsync(t => t.CategoryBudgetTargetId == request.TargetId);
            if (target is null)
                throw new InvalidOperationException("Budget target was not found for the current user.");

            if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, target.SharedBudgetId, target.UserId))
                throw new InvalidOperationException("Budget target was not found for the current user.");
        }

        var writableSharedBudgetIds = await _sharedBudgets.GetFinancialManagerSharedBudgetIdsAsync(userId);
        target ??= await _db.CategoryBudgetTargets
            .FirstOrDefaultAsync(t =>
                (t.SharedBudgetId.HasValue
                    ? writableSharedBudgetIds.Contains(t.SharedBudgetId.Value)
                    : t.UserId == userId) &&
                t.CategoryId == request.CategoryId &&
                t.BudgetMonth == monthStart);

        if (target is null)
        {
            target = new CategoryBudgetTarget
            {
                UserId = userId,
                SharedBudgetId = category.SharedBudgetId ?? await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId),
                CategoryId = request.CategoryId,
                BudgetMonth = monthStart
            };
            _db.CategoryBudgetTargets.Add(target);
        }
        else if (target.CategoryId != request.CategoryId || target.BudgetMonth != monthStart)
        {
            var duplicateExists = await _db.CategoryBudgetTargets.AnyAsync(t =>
                t.CategoryBudgetTargetId != target.CategoryBudgetTargetId &&
                (t.SharedBudgetId.HasValue
                    ? writableSharedBudgetIds.Contains(t.SharedBudgetId.Value)
                    : t.UserId == userId) &&
                t.CategoryId == request.CategoryId &&
                t.BudgetMonth == monthStart);
            if (duplicateExists)
                throw new InvalidOperationException("A budget already exists for that category and month.");

            target.CategoryId = request.CategoryId;
            target.BudgetMonth = monthStart;
        }

        target.PlannedAmount = plannedAmount;
        target.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTargetAsync(int userId, int targetId)
    {
        var target = await _db.CategoryBudgetTargets
            .FirstOrDefaultAsync(t => t.CategoryBudgetTargetId == targetId);
        if (target is null)
            return;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, target.SharedBudgetId, target.UserId))
            return;

        _db.CategoryBudgetTargets.Remove(target);
        await _db.SaveChangesAsync();
    }

    private CategoryBudgetMonthRow CreateRow(
        int? targetId,
        int categoryId,
        string categoryName,
        decimal plannedAmount,
        decimal actualAmount)
    {
        plannedAmount = CurrencyPolicy.Round(plannedAmount);
        actualAmount = CurrencyPolicy.Round(actualAmount);
        var remaining = CurrencyPolicy.Round(plannedAmount - actualAmount);
        var percentUsed = plannedAmount > 0 ? CurrencyPolicy.Round(actualAmount / plannedAmount * 100m) : 0m;
        var status = plannedAmount <= 0
            ? CategoryBudgetAlertStatus.None
            : actualAmount > plannedAmount
                ? CategoryBudgetAlertStatus.Overspent
                : percentUsed >= _thresholds.WarningPercent
                    ? CategoryBudgetAlertStatus.Warning
                    : CategoryBudgetAlertStatus.None;

        return new CategoryBudgetMonthRow(
            targetId,
            categoryId,
            categoryName,
            plannedAmount,
            actualAmount,
            remaining,
            percentUsed,
            status);
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static string GetCategoryName(Category? category) =>
        string.IsNullOrWhiteSpace(category?.CategoryName)
            ? InsightsDataService.UncategorizedCategoryName
            : category.CategoryName.Trim();
}
