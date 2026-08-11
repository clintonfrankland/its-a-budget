using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed record OnboardingState(
    bool IsCompleted,
    int Step,
    int? SharedBudgetId,
    string BudgetName,
    IReadOnlyList<SharedBudgetMembershipSummary> AvailableBudgets,
    IReadOnlyList<Account> Accounts);

public class OnboardingService
{
    public const int WelcomeStep = 0;
    public const int BudgetStep = 1;
    public const int AccountStep = 2;
    public const int CategoriesStep = 3;
    public const int SharingStep = 4;
    public const int ReviewStep = 5;

    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;
    private readonly AccountsDataService _accounts;
    private readonly BudgetInviteService _invites;

    public OnboardingService(
        ClintonFranklandDbContext db,
        SharedBudgetDataService sharedBudgets,
        AccountsDataService accounts,
        BudgetInviteService invites)
    {
        _db = db;
        _sharedBudgets = sharedBudgets;
        _accounts = accounts;
        _invites = invites;
    }

    public async Task<bool> ShouldOfferAsync(int userId) =>
        await _db.Users.AsNoTracking().AnyAsync(user =>
            user.UserId == userId && !user.IsDeleted && !user.OnboardingCompleted);

    public async Task<OnboardingState?> GetStateAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(candidate =>
            candidate.UserId == userId && !candidate.IsDeleted);
        if (user is null)
            return null;

        var budgets = await _sharedBudgets.GetReadableSharedBudgetSummariesAsync(userId);
        var activeBudgetId = await _sharedBudgets.GetActiveSharedBudgetIdAsync(userId);
        var activeBudget = budgets.FirstOrDefault(budget => budget.SharedBudgetId == activeBudgetId);
        var accounts = activeBudgetId.HasValue
            ? await _db.Accounts.AsNoTracking()
                .Where(account => account.SharedBudgetId == activeBudgetId && !(account.IsDeleted ?? false))
                .OrderBy(account => account.AccountName)
                .ToListAsync()
            : [];

        return new OnboardingState(
            user.OnboardingCompleted,
            Math.Clamp(user.OnboardingStep, WelcomeStep, ReviewStep),
            activeBudgetId,
            activeBudget?.Name ?? string.Empty,
            budgets,
            accounts);
    }

    public Task SaveStepAsync(int userId, int step) => UpdateUserAsync(userId, user =>
        user.OnboardingStep = Math.Clamp(step, WelcomeStep, ReviewStep));

    public async Task<int> SaveBudgetAsync(int userId, int? sharedBudgetId, string budgetName)
    {
        var normalizedName = (budgetName ?? string.Empty).Trim();
        if (normalizedName.Length is < 1 or > 128)
            throw new InvalidOperationException("Enter a budget name between 1 and 128 characters.");

        int selectedBudgetId;
        if (sharedBudgetId.HasValue)
        {
            if (!await _sharedBudgets.SetActiveSharedBudgetAsync(userId, sharedBudgetId.Value))
                throw new InvalidOperationException("That budget is no longer available.");
            selectedBudgetId = sharedBudgetId.Value;
        }
        else
        {
            selectedBudgetId = await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId)
                ?? throw new InvalidOperationException("We could not create your budget. Please try again.");
        }

        var canRename = await _sharedBudgets.CanManageMembersAsync(userId, selectedBudgetId);
        if (canRename)
        {
            var budget = await _db.SharedBudgets.FirstAsync(candidate => candidate.SharedBudgetId == selectedBudgetId);
            budget.Name = normalizedName;
            budget.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        await SaveStepAsync(userId, AccountStep);
        return selectedBudgetId;
    }

    public async Task<int> SaveAccountAsync(
        int userId,
        string accountName,
        int accountTypeId,
        decimal openingBalance)
    {
        var normalizedName = (accountName ?? string.Empty).Trim();
        if (normalizedName.Length is < 1 or > 255)
            throw new InvalidOperationException("Enter an account name between 1 and 255 characters.");
        if (accountTypeId is < 1 or > 5)
            throw new InvalidOperationException("Choose an account type.");

        await _accounts.SaveAccountAsync(
            userId, -1, normalizedName, string.Empty, accountTypeId, openingBalance,
            0m, 0m, 1, 0m, 0m, string.Empty, DateTime.UtcNow);
        var activeBudgetId = await _sharedBudgets.GetActiveSharedBudgetIdAsync(userId);
        var account = await _db.Accounts
            .Where(candidate => candidate.SharedBudgetId == activeBudgetId && !(candidate.IsDeleted ?? false))
            .OrderByDescending(candidate => candidate.AccountId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The account could not be saved. Please try again.");
        await _accounts.SetDefaultAccountAsync(userId, account.AccountId, DateTime.UtcNow);
        await SaveStepAsync(userId, CategoriesStep);
        return account.AccountId;
    }

    public async Task SaveCategoriesAsync(int userId, IEnumerable<string> categoryNames)
    {
        var sharedBudgetId = await _sharedBudgets.GetActiveSharedBudgetIdAsync(userId)
            ?? throw new InvalidOperationException("Select a budget before adding categories.");
        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, sharedBudgetId, userId))
            throw new UnauthorizedAccessException("You do not have permission to add categories to this budget.");

        var names = categoryNames
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        if (names.Any(name => name.Length > 50))
            throw new InvalidOperationException("Category names must be 50 characters or fewer.");

        var existing = await _db.Categories
            .Where(category => category.SharedBudgetId == sharedBudgetId)
            .Select(category => category.CategoryName)
            .ToListAsync();
        foreach (var name in names.Where(name => !existing.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            _db.Categories.Add(new Category { CategoryName = name, UserId = userId, SharedBudgetId = sharedBudgetId });
        }
        await _db.SaveChangesAsync();
        await SaveStepAsync(userId, SharingStep);
    }

    public async Task<BudgetInviteCreateResult> SaveInviteAsync(int userId, string target)
    {
        var sharedBudgetId = await _sharedBudgets.GetActiveSharedBudgetIdAsync(userId)
            ?? throw new InvalidOperationException("Select a budget before inviting someone.");
        var result = await _invites.CreateInviteAsync(userId, sharedBudgetId, target, BudgetMemberRole.Editor);
        await SaveStepAsync(userId, ReviewStep);
        return result;
    }

    public Task CompleteAsync(int userId) => UpdateUserAsync(userId, user =>
    {
        user.OnboardingCompleted = true;
        user.OnboardingStep = ReviewStep;
    });

    private async Task UpdateUserAsync(int userId, Action<User> update)
    {
        var user = await _db.Users.FirstOrDefaultAsync(candidate => candidate.UserId == userId && !candidate.IsDeleted)
            ?? throw new InvalidOperationException("Your user profile could not be loaded.");
        update(user);
        await _db.SaveChangesAsync();
    }
}
