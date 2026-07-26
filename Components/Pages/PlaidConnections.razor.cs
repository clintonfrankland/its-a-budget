using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class PlaidConnections
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUser { get; set; } = default!;
    [Inject] private PlaidConnectionService PlaidConnectionData { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private readonly List<PlaidItemSummary> items = [];
    private string errorMessage = string.Empty;
    private string? linkToken;

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

    private async Task CreateLinkTokenAsync()
    {
        try
        {
            linkToken = (await PlaidConnectionData.CreateLinkTokenAsync(CurrentUser.UserId, CancellationToken.None)).Token;
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
    }

    private async Task CreateUpdateLinkTokenAsync(int plaidItemId)
    {
        try
        {
            linkToken = (await PlaidConnectionData.CreateUpdateLinkTokenAsync(CurrentUser.UserId, plaidItemId, CancellationToken.None)).Token;
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
}
