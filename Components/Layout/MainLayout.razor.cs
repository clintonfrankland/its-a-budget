using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace ClintonFrankland.Components.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private StartupDiagnosticsState StartupDiagnostics { get; set; } = default!;
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private CurrentUserContext CurrentUserContext { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private SiteInfoService SiteInfoService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    private List<SharedBudgetMembershipSummary> _budgetSwitcherOptions = [];
    private int _selectedSwitcherBudgetId;

    protected override void OnInitialized()
    {
        // Subscribe to navigation events to collapse navbar on navigation
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AuthService.InitializeAsync();
            if (AuthService.IsAuthenticated)
            {
                // Layout and routed page first renders can overlap in a Blazor circuit. Use an
                // isolated scope so their EF queries never share the circuit-scoped DbContext.
                await using var scope = ScopeFactory.CreateAsyncScope();
                var sharedBudgetsService = scope.ServiceProvider.GetRequiredService<SharedBudgetDataService>();
                _budgetSwitcherOptions = await sharedBudgetsService.GetReadableSharedBudgetSummariesAsync(CurrentUserContext.UserId);
                _selectedSwitcherBudgetId = _budgetSwitcherOptions.FirstOrDefault()?.SharedBudgetId ?? 0;
            }
            StateHasChanged();
        }
    }

    private Task CollapseNavbar() => TryCollapseNavbarAsync(JS, "navbar");

    internal static async Task TryCollapseNavbarAsync(IJSRuntime js, string elementId)
    {
        try
        {
            await js.InvokeVoidAsync("budgetApp.collapseNavbar", elementId);
        }
        catch (InvalidOperationException)
        {
            // JavaScript is unavailable during static prerendering.
        }
        catch (JSDisconnectedException)
        {
            // The circuit can disconnect while a navigation event is being handled.
        }
        catch (JSException)
        {
            // Navbar collapse is cosmetic. A stale or unavailable script must never
            // terminate the circuit and interrupt the destination page's data load.
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _ = InvokeAsync(async () =>
        {
            await CollapseNavbar();
            StateHasChanged();
        });
    }

    private async Task HandleLogoutClick()
    {
        await CollapseNavbar();
        if (AuthService.IsAuthenticated)
        {
            await AuthService.LogoutAsync();
            Navigation.NavigateTo($"/auth/logout?returnUrl={Uri.EscapeDataString("/")}", forceLoad: true);
        }
    }

    private async Task HandleBudgetSwitcherChange(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var sharedBudgetId))
            _selectedSwitcherBudgetId = sharedBudgetId;

        await CollapseNavbar();
        Navigation.NavigateTo("sharing");
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
