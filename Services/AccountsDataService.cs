using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class AccountsDataService
{
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
        var roundedBalance = CurrencyPolicy.RoundNonNegativeSqlAmount(balance);
        var roundedCreditLimit = CurrencyPolicy.RoundNonNegativeSqlAmount(creditLimit);
        var roundedAvailableCredit = CurrencyPolicy.RoundNonNegativeSqlAmount(availableCredit);
        var roundedMinimumPayment = CurrencyPolicy.RoundNonNegativeSqlAmount(minimumPayment);
        var roundedInterestRate = CurrencyPolicy.RoundNonNegativeSqlAmount(interestRate);
        var isNew = accountId == -1;
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

        var postedAmount = isNew
            ? 0m
            : await _db.Transactions
                .Where(t => t.AccountId == account.AccountId)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var clearedPostedAmount = isNew
            ? 0m
            : await _db.Transactions
                .Where(t => t.AccountId == account.AccountId && t.Cleared)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        account.AccountName = accountName;
        account.AccountNumber = accountNumber;
        account.AccountTypeId = accountTypeId;
        account.Balance = roundedBalance;
        account.BeginningBalance = CurrencyPolicy.Round(roundedBalance - postedAmount);
        account.ClearedBalance = CurrencyPolicy.Round(account.BeginningBalance + clearedPostedAmount);
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
            _db.Accounts.Add(account);

        await _db.SaveChangesAsync();
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
