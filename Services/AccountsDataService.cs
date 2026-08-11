using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class AccountsDataService
{
    public sealed record BalanceAdjustmentPreview(
        decimal CurrentOpeningBalance,
        decimal ProposedOpeningBalance,
        decimal CurrentBalance,
        decimal ProposedBalance,
        decimal CurrentClearedBalance,
        decimal ProposedClearedBalance);

    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public AccountsDataService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public async Task<List<Account>> GetAccountsForUserAsync(int userId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Accounts
            .AsNoTracking()
            .Include(a => a.AccountType)
            .Where(a =>
                (a.IsDeleted == null || a.IsDeleted == false) &&
                (a.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(a.SharedBudgetId.Value)
                    : a.UserId == userId))
            .OrderBy(a => a.AccountName)
            .ToListAsync();
    }

    public async Task<bool> SetDefaultAccountAsync(int userId, int accountId, DateTime lastUpdated)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a =>
            a.AccountId == accountId && !(a.IsDeleted ?? false));
        if (account is null ||
            !await _sharedBudgets.CanManageFinancialDataAsync(userId, account.SharedBudgetId, account.UserId))
        {
            return false;
        }

        var scope = _db.Accounts.Where(a => !(a.IsDeleted ?? false) &&
            (account.SharedBudgetId.HasValue
                ? a.SharedBudgetId == account.SharedBudgetId
                : !a.SharedBudgetId.HasValue && a.UserId == account.UserId));
        if (_db.Database.IsRelational())
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            await scope.Where(a => a.IsDefault && a.AccountId != accountId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(a => a.IsDefault, false)
                    .SetProperty(a => a.LastUpdated, lastUpdated));
            account.IsDefault = true;
            account.LastUpdated = lastUpdated;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        var previousDefaults = await scope
            .Where(a => a.IsDefault && a.AccountId != accountId)
            .ToListAsync();
        foreach (var previousDefault in previousDefaults)
        {
            previousDefault.IsDefault = false;
            previousDefault.LastUpdated = lastUpdated;
        }
        account.IsDefault = true;
        account.LastUpdated = lastUpdated;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<Account?> GetAccountByIdAsync(int userId, int accountId)
    {
        var sharedBudgetIds = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        return await _db.Accounts
            .AsNoTracking()
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync(a =>
                a.AccountId == accountId &&
                (a.SharedBudgetId.HasValue
                    ? sharedBudgetIds.Contains(a.SharedBudgetId.Value)
                    : a.UserId == userId));
    }

    public async Task SaveAccountAsync(
        int userId,
        int accountId,
        string accountName,
        string accountNumber,
        int accountTypeId,
        decimal balance,
        decimal creditLimit,
        decimal availableCredit,
        int dueDate,
        decimal minimumPayment,
        decimal interestRate,
        string webUrl,
        DateTime lastUpdated)
    {
        var isNew = accountId == -1;
        var roundedOpeningBalance = isNew
            ? CurrencyPolicy.RoundNonNegativeSqlAmount(balance)
            : 0m;
        var roundedCreditLimit = CurrencyPolicy.RoundNonNegativeSqlAmount(creditLimit);
        var roundedAvailableCredit = CurrencyPolicy.RoundNonNegativeSqlAmount(availableCredit);
        var roundedMinimumPayment = CurrencyPolicy.RoundNonNegativeSqlAmount(minimumPayment);
        var roundedInterestRate = CurrencyPolicy.RoundNonNegativeSqlAmount(interestRate);
        var account = isNew
            ? new Account
            {
                BeginningBalance = 0m,
                ClearedBalance = 0m,
                IsDefault = false,
                UserId = userId,
                SharedBudgetId = await _sharedBudgets.GetDefaultSharedBudgetIdAsync(userId)
            }
            : await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);

        if (account is null)
            return;

        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, account.SharedBudgetId, account.UserId))
            return;

        account.AccountName = accountName;
        account.AccountNumber = accountNumber;
        account.AccountTypeId = accountTypeId;
        if (isNew)
        {
            account.Balance = roundedOpeningBalance;
            account.BeginningBalance = roundedOpeningBalance;
            account.ClearedBalance = roundedOpeningBalance;
        }
        account.CreditLimit = roundedCreditLimit;
        account.AvailableCredit = roundedAvailableCredit;
        account.DueDate = dueDate;
        account.MinimumPayment = roundedMinimumPayment;
        account.InterestRate = roundedInterestRate;
        account.WebUrl = webUrl;
        account.LastUpdated = lastUpdated;

        account.Balance = CurrencyPolicy.Round(account.Balance);
        account.BeginningBalance = CurrencyPolicy.Round(account.BeginningBalance);
        account.ClearedBalance = CurrencyPolicy.Round(account.ClearedBalance);
        account.CreditLimit = CurrencyPolicy.Round(account.CreditLimit);
        account.AvailableCredit = CurrencyPolicy.Round(account.AvailableCredit);
        account.MinimumPayment = CurrencyPolicy.Round(account.MinimumPayment);
        account.InterestRate = CurrencyPolicy.Round(account.InterestRate);

        if (isNew)
        {
            account.IsDefault = !await _db.Accounts.AnyAsync(a =>
                !(a.IsDeleted ?? false) &&
                (account.SharedBudgetId.HasValue
                    ? a.SharedBudgetId == account.SharedBudgetId
                    : !a.SharedBudgetId.HasValue && a.UserId == account.UserId));
            _db.Accounts.Add(account);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<BalanceAdjustmentPreview?> GetBalanceAdjustmentPreviewAsync(
        int userId,
        int accountId,
        decimal proposedOpeningBalance)
    {
        var roundedOpeningBalance = CurrencyPolicy.RoundSignedSqlAmount(proposedOpeningBalance);
        var account = await GetManageableAccountAsync(userId, accountId);
        if (account is null)
            return null;

        var transactions = _db.TransactionsForAccount(account);
        var postedAmount = await transactions.SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var clearedPostedAmount = await transactions
            .Where(t => t.Cleared)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return new BalanceAdjustmentPreview(
            CurrencyPolicy.Round(account.BeginningBalance),
            roundedOpeningBalance,
            CurrencyPolicy.Round(account.BeginningBalance + postedAmount),
            CurrencyPolicy.Round(roundedOpeningBalance + postedAmount),
            CurrencyPolicy.Round(account.BeginningBalance + clearedPostedAmount),
            CurrencyPolicy.Round(roundedOpeningBalance + clearedPostedAmount));
    }

    public async Task<bool> AdjustOpeningBalanceAsync(
        int userId,
        int accountId,
        decimal proposedOpeningBalance,
        DateTime lastUpdated)
    {
        var preview = await GetBalanceAdjustmentPreviewAsync(userId, accountId, proposedOpeningBalance);
        if (preview is null)
            return false;

        var account = await _db.Accounts.FirstAsync(a => a.AccountId == accountId);
        account.BeginningBalance = preview.ProposedOpeningBalance;
        account.Balance = preview.ProposedBalance;
        account.ClearedBalance = preview.ProposedClearedBalance;
        account.LastUpdated = lastUpdated;
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<Account?> GetManageableAccountAsync(int userId, int accountId)
    {
        var account = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == accountId);
        if (account is null ||
            !await _sharedBudgets.CanManageFinancialDataAsync(userId, account.SharedBudgetId, account.UserId))
        {
            return null;
        }

        return account;
    }

    public async Task DeleteAccountAsync(int userId, int accountId)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
        if (account is null) return;
        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, account.SharedBudgetId, account.UserId))
            return;

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
    }
}
