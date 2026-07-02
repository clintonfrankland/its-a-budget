using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class AccountsDataService
{
    private readonly ClintonFranklandDbContext _db;

    public AccountsDataService(ClintonFranklandDbContext db)
    {
        _db = db;
    }

    public Task<List<Account>> GetAccountsForUserAsync(int userId) =>
        _db.Accounts
            .AsNoTracking()
            .Include(a => a.AccountType)
            .Where(a => a.UserId == userId && (a.IsDeleted == null || a.IsDeleted == false))
            .OrderBy(a => a.AccountName)
            .ToListAsync();

    public Task<Account?> GetAccountByIdAsync(int userId, int accountId) =>
        _db.Accounts
            .AsNoTracking()
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync(a => a.AccountId == accountId && a.UserId == userId);

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
        var account = isNew
            ? new Account
            {
                BeginningBalance = 0m,
                ClearedBalance = 0m,
                IsDefault = false,
                UserId = userId
            }
            : await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId && a.UserId == userId);

        if (account is null)
            return;

        account.AccountName = accountName;
        account.AccountNumber = accountNumber;
        account.AccountTypeId = accountTypeId;
        account.Balance = balance;
        account.CreditLimit = creditLimit;
        account.AvailableCredit = availableCredit;
        account.DueDate = dueDate;
        account.MinimumPayment = minimumPayment;
        account.InterestRate = interestRate;
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
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId && a.UserId == userId);
        if (account is null) return;
        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
    }
}
