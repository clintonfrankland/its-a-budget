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
            .Include(a => a.AccountType)
            .Where(a => a.UserId == userId && (a.IsDeleted == null || a.IsDeleted == false))
            .OrderBy(a => a.AccountName)
            .ToListAsync();

    public Task<Account?> GetAccountByIdAsync(int accountId) =>
        _db.Accounts
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync(a => a.AccountId == accountId);

    public async Task SaveAccountAsync(Account account, bool isNew)
    {
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

    public async Task DeleteAccountAsync(int accountId)
    {
        var account = await _db.Accounts.FindAsync(accountId);
        if (account is null) return;
        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
    }
}
