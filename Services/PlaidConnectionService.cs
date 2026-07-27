using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed record PlaidItemSummary(int PlaidItemId, string InstitutionName, string Status, DateTime UpdatedAtUtc, int MappingCount);
public sealed record PlaidConnectionResult(int PlaidItemId, IReadOnlyList<PlaidDiscoveredAccount> Accounts);
public sealed record PlaidAccountMappingCandidate(string PlaidAccountId, string Name, string? Mask, string Type, string Subtype, int? BudgetAccountId);
public sealed record ManageableBudgetAccount(int AccountId, string Name);

public sealed class PlaidConnectionService
{
    private readonly ClintonFranklandDbContext _database;
    private readonly SharedBudgetDataService _sharedBudgets;
    private readonly IPlaidClient _plaidClient;
    private readonly IDataProtector _accessTokenProtector;

    public PlaidConnectionService(ClintonFranklandDbContext database, SharedBudgetDataService sharedBudgets,
        IPlaidClient plaidClient, IDataProtectionProvider dataProtectionProvider)
    {
        _database = database;
        _sharedBudgets = sharedBudgets;
        _plaidClient = plaidClient;
        _accessTokenProtector = dataProtectionProvider.CreateProtector("BudgetApp.Plaid.AccessToken.v1");
    }

    public Task<PlaidLinkToken> CreateLinkTokenAsync(int userId, CancellationToken cancellationToken) =>
        _plaidClient.CreateLinkTokenAsync(userId, false, null, cancellationToken);

    public async Task<PlaidLinkToken> CreateUpdateLinkTokenAsync(int userId, int plaidItemId, CancellationToken cancellationToken)
    {
        var item = await GetOwnedItemAsync(userId, plaidItemId, cancellationToken);
        return await _plaidClient.CreateLinkTokenAsync(userId, true, _accessTokenProtector.Unprotect(item.EncryptedAccessToken), cancellationToken);
    }

    public async Task<PlaidConnectionResult> ExchangePublicTokenAsync(int userId, string publicToken, string? institutionId,
        string? institutionName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
            throw new ArgumentException("A Plaid public token is required.", nameof(publicToken));

        var exchanged = await _plaidClient.ExchangePublicTokenAsync(publicToken, cancellationToken);
        var accounts = await _plaidClient.GetAccountsAsync(exchanged.AccessToken, cancellationToken);
        var now = DateTime.UtcNow;
        var item = await _database.PlaidItems.SingleOrDefaultAsync(item => item.ItemId == exchanged.ItemId, cancellationToken);
        if (item is not null && item.UserId != userId)
            throw new InvalidOperationException("This Plaid connection is already linked to a Budget user.");

        if (item is null)
        {
            item = new PlaidItem
            {
                UserId = userId,
                ItemId = exchanged.ItemId,
                EncryptedAccessToken = _accessTokenProtector.Protect(exchanged.AccessToken),
                InstitutionId = institutionId?.Trim(),
                InstitutionName = institutionName?.Trim(),
                Status = PlaidItemStatus.Active,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _database.PlaidItems.Add(item);
        }
        else
        {
            // Reconnecting an Item through the initial Link completion path can return a
            // credential; update-mode Link must instead use CompleteUpdateAsync below.
            item.EncryptedAccessToken = _accessTokenProtector.Protect(exchanged.AccessToken);
            item.InstitutionId = institutionId?.Trim() ?? item.InstitutionId;
            item.InstitutionName = institutionName?.Trim() ?? item.InstitutionName;
            item.Status = PlaidItemStatus.Active;
            item.DisconnectedAtUtc = null;
            item.UpdatedAtUtc = now;
        }
        await _database.SaveChangesAsync(cancellationToken);
        return new PlaidConnectionResult(item.PlaidItemId, accounts);
    }

    /// <summary>
    /// Completes an update-mode Link session. Plaid retains an Item's access token in
    /// update mode, so no public-token exchange or credential replacement occurs here.
    /// </summary>
    public async Task<PlaidConnectionResult> CompleteUpdateAsync(int userId, int plaidItemId, string? institutionId,
        string? institutionName, CancellationToken cancellationToken)
    {
        var item = await GetOwnedItemAsync(userId, plaidItemId, cancellationToken);
        var accounts = await _plaidClient.GetAccountsAsync(_accessTokenProtector.Unprotect(item.EncryptedAccessToken), cancellationToken);
        item.InstitutionId = institutionId?.Trim() ?? item.InstitutionId;
        item.InstitutionName = institutionName?.Trim() ?? item.InstitutionName;
        item.Status = PlaidItemStatus.Active;
        item.DisconnectedAtUtc = null;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _database.SaveChangesAsync(cancellationToken);
        return new PlaidConnectionResult(item.PlaidItemId, accounts);
    }

    public async Task MapAccountAsync(int userId, int plaidItemId, string plaidAccountId, int budgetAccountId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plaidAccountId))
            throw new ArgumentException("A Plaid account id is required.", nameof(plaidAccountId));

        var item = await GetOwnedItemAsync(userId, plaidItemId, cancellationToken);
        if (item.Status != PlaidItemStatus.Active)
            throw new InvalidOperationException("Only active Plaid Items can be mapped.");

        // Never trust a Plaid account id supplied by the browser. Discover it from this
        // Item's protected access token immediately before creating the association.
        var discoveredAccounts = await _plaidClient.GetAccountsAsync(_accessTokenProtector.Unprotect(item.EncryptedAccessToken), cancellationToken);
        if (!discoveredAccounts.Any(account => string.Equals(account.AccountId, plaidAccountId, StringComparison.Ordinal)))
            throw new InvalidOperationException("The Plaid account does not belong to this connection.");

        var budgetAccount = await _database.Accounts.SingleOrDefaultAsync(account => account.AccountId == budgetAccountId, cancellationToken)
            ?? throw new InvalidOperationException("The selected Budget account does not exist.");
        if (!await _sharedBudgets.CanManageFinancialDataAsync(userId, budgetAccount.SharedBudgetId, budgetAccount.UserId))
            throw new UnauthorizedAccessException("You may only map Plaid accounts to Budget accounts you can manage.");

        var existing = await _database.PlaidAccountMappings
            .SingleOrDefaultAsync(mapping => mapping.PlaidItemId == plaidItemId && mapping.PlaidAccountId == plaidAccountId, cancellationToken);
        if (existing is null)
        {
            _database.PlaidAccountMappings.Add(new PlaidAccountMapping
            {
                PlaidItemId = plaidItemId,
                PlaidAccountId = plaidAccountId,
                BudgetAccountId = budgetAccountId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.BudgetAccountId = budgetAccountId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task DisconnectAsync(int userId, int plaidItemId, CancellationToken cancellationToken)
    {
        var item = await GetOwnedItemAsync(userId, plaidItemId, cancellationToken);
        if (item.Status == PlaidItemStatus.Disconnected)
            return;
        await _plaidClient.RemoveItemAsync(_accessTokenProtector.Unprotect(item.EncryptedAccessToken), cancellationToken);
        item.Status = PlaidItemStatus.Disconnected;
        item.DisconnectedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _database.SaveChangesAsync(cancellationToken);
    }

    public Task<List<PlaidItemSummary>> GetItemsAsync(int userId, CancellationToken cancellationToken) =>
        _database.PlaidItems.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new PlaidItemSummary(item.PlaidItemId, item.InstitutionName ?? "Connected institution", item.Status,
                item.UpdatedAtUtc, item.AccountMappings.Count)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlaidAccountMappingCandidate>> GetDiscoveredAccountsAsync(int userId, int plaidItemId, CancellationToken cancellationToken)
    {
        var item = await GetOwnedItemAsync(userId, plaidItemId, cancellationToken);
        var mappings = await _database.PlaidAccountMappings.AsNoTracking()
            .Where(mapping => mapping.PlaidItemId == plaidItemId)
            .ToDictionaryAsync(mapping => mapping.PlaidAccountId, mapping => mapping.BudgetAccountId, StringComparer.Ordinal, cancellationToken);
        var accounts = await _plaidClient.GetAccountsAsync(_accessTokenProtector.Unprotect(item.EncryptedAccessToken), cancellationToken);
        return accounts.Select(account => new PlaidAccountMappingCandidate(account.AccountId, account.Name, account.Mask,
            account.Type, account.Subtype, mappings.GetValueOrDefault(account.AccountId))).ToList();
    }

    public async Task<IReadOnlyList<ManageableBudgetAccount>> GetManageableBudgetAccountsAsync(int userId, CancellationToken cancellationToken)
    {
        var accounts = await _database.Accounts.AsNoTracking().Where(account => account.IsDeleted != true).OrderBy(account => account.AccountName).ToListAsync(cancellationToken);
        var result = new List<ManageableBudgetAccount>();
        foreach (var account in accounts)
        {
            if (await _sharedBudgets.CanManageFinancialDataAsync(userId, account.SharedBudgetId, account.UserId))
                result.Add(new ManageableBudgetAccount(account.AccountId, account.AccountName));
        }
        return result;
    }

    private async Task<PlaidItem> GetOwnedItemAsync(int userId, int plaidItemId, CancellationToken cancellationToken) =>
        await _database.PlaidItems.SingleOrDefaultAsync(item => item.PlaidItemId == plaidItemId && item.UserId == userId, cancellationToken)
        ?? throw new UnauthorizedAccessException("The requested Plaid Item is not available to this user.");
}
