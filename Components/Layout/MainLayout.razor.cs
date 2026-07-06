using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace ClintonFrankland.Components.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private StartupDiagnosticsState StartupDiagnostics { get; set; } = default!;
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private CurrentUserContext CurrentUserContext { get; set; } = default!;

    [Inject]
    private SharedBudgetDataService SharedBudgetsService { get; set; } = default!;

    [Inject]
    private SiteInfoService SiteInfoService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private bool _navbarExpanded = false;
    private List<SharedBudgetMembershipSummary> _budgetSwitcherOptions = [];
    private int _selectedSwitcherBudgetId;

    // CSS class for navbar collapse state
    private string NavbarCollapseClass => _navbarExpanded ? "collapse show" : "collapse";

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
                _budgetSwitcherOptions = await SharedBudgetsService.GetReadableSharedBudgetSummariesAsync(CurrentUserContext.UserId);
                _selectedSwitcherBudgetId = _budgetSwitcherOptions.FirstOrDefault()?.SharedBudgetId ?? 0;
            }
            StateHasChanged();
        }
    }

    private void ToggleNavbar()
    {
        _navbarExpanded = !_navbarExpanded;
    }

    private void CollapseNavbar()
    {
        if (_navbarExpanded)
        {
            _navbarExpanded = false;
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // Collapse navbar when navigating to a new page
        if (_navbarExpanded)
        {
            _navbarExpanded = false;
            StateHasChanged();
        }
    }

    private string GetIconClass()
    {
        var icon = SiteInfoService.SiteInfo.Icon;
        if (icon.Contains("<i class='"))
        {
            var start = icon.IndexOf("'") + 1;
            var end = icon.IndexOf("'", start);
            return icon.Substring(start, end - start);
        }
        return "fa fa-address-card";
    }

    private async Task HandleLogoutClick()
    {
        CollapseNavbar();
        if (AuthService.IsAuthenticated)
        {
            await AuthService.LogoutAsync();
            Navigation.NavigateTo($"/auth/logout?returnUrl={Uri.EscapeDataString("/")}", forceLoad: true);
        }
    }

    private void HandleBudgetSwitcherChange(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var sharedBudgetId))
            _selectedSwitcherBudgetId = sharedBudgetId;

        CollapseNavbar();
        Navigation.NavigateTo("sharing");
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
