using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ClintonFrankland.Components.Pages;

public partial class PlaidConnections
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUser { get; set; } = default!;
    [Inject] private PlaidConnectionService PlaidConnectionData { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private readonly List<PlaidItemSummary> items = [];
    private string errorMessage = string.Empty;
    private int? selectedPlaidItemId;
    private readonly List<PlaidAccountMappingCandidate> discoveredAccounts = [];
    private readonly List<ManageableBudgetAccount> manageableBudgetAccounts = [];
    private readonly Dictionary<string, int?> selectedBudgetAccounts = new(StringComparer.Ordinal);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/accounts/plaid")}");
            return;
        }
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        items.Clear();
        items.AddRange(await PlaidConnectionData.GetItemsAsync(CurrentUser.UserId, CancellationToken.None));
    }

    private async Task StartConnectionAsync()
    {
        try
        {
            var linkToken = (await PlaidConnectionData.CreateLinkTokenAsync(CurrentUser.UserId, CancellationToken.None)).Token;
            await OpenLinkAndExchangeAsync(linkToken);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
    }

    private async Task StartUpdateConnectionAsync(int plaidItemId)
    {
        try
        {
            var linkToken = (await PlaidConnectionData.CreateUpdateLinkTokenAsync(CurrentUser.UserId, plaidItemId, CancellationToken.None)).Token;
            await OpenLinkAndExchangeAsync(linkToken);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
    }

    private async Task OpenLinkAndExchangeAsync(string linkToken)
    {
        errorMessage = string.Empty;
        var result = await Js.InvokeAsync<PlaidLinkResult?>("plaidLink.open", linkToken);
        if (result is null)
            return;

        var connection = await PlaidConnectionData.ExchangePublicTokenAsync(CurrentUser.UserId, result.PublicToken,
            result.InstitutionId, result.InstitutionName, CancellationToken.None);
        selectedPlaidItemId = connection.PlaidItemId;
        discoveredAccounts.Clear();
        discoveredAccounts.AddRange(connection.Accounts.Select(account => new PlaidAccountMappingCandidate(account.AccountId,
            account.Name, account.Mask, account.Type, account.Subtype, null)));
        selectedBudgetAccounts.Clear();
        await LoadManageableBudgetAccountsAsync();
        await LoadAsync();
    }

    private async Task ShowAccountsAsync(int plaidItemId)
    {
        try
        {
            errorMessage = string.Empty;
            selectedPlaidItemId = plaidItemId;
            discoveredAccounts.Clear();
            discoveredAccounts.AddRange(await PlaidConnectionData.GetDiscoveredAccountsAsync(CurrentUser.UserId, plaidItemId, CancellationToken.None));
            selectedBudgetAccounts.Clear();
            foreach (var account in discoveredAccounts)
                selectedBudgetAccounts[account.PlaidAccountId] = account.BudgetAccountId;
            await LoadManageableBudgetAccountsAsync();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
    }

    private async Task LoadManageableBudgetAccountsAsync()
    {
        manageableBudgetAccounts.Clear();
        manageableBudgetAccounts.AddRange(await PlaidConnectionData.GetManageableBudgetAccountsAsync(CurrentUser.UserId, CancellationToken.None));
    }

    private void SelectBudgetAccount(string plaidAccountId, string? value) =>
        selectedBudgetAccounts[plaidAccountId] = int.TryParse(value, out var budgetAccountId) ? budgetAccountId : null;

    private int? GetSelectedBudgetAccountId(string plaidAccountId) =>
        selectedBudgetAccounts.GetValueOrDefault(plaidAccountId);

    private string GetSelectedBudgetAccount(string plaidAccountId) =>
        GetSelectedBudgetAccountId(plaidAccountId)?.ToString() ?? string.Empty;

    private async Task MapAccountAsync(string plaidAccountId)
    {
        if (selectedPlaidItemId is not int plaidItemId || GetSelectedBudgetAccountId(plaidAccountId) is not int budgetAccountId)
            return;
        try
        {
            errorMessage = string.Empty;
            await PlaidConnectionData.MapAccountAsync(CurrentUser.UserId, plaidItemId, plaidAccountId, budgetAccountId, CancellationToken.None);
            await ShowAccountsAsync(plaidItemId);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
    }

    private async Task DisconnectAsync(int plaidItemId)
    {
        try
        {
            await PlaidConnectionData.DisconnectAsync(CurrentUser.UserId, plaidItemId, CancellationToken.None);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
    }

    private void BackToAccounts() => Navigation.NavigateTo("/accounts");

    private sealed record PlaidLinkResult(string PublicToken, string? InstitutionId, string? InstitutionName);
}
