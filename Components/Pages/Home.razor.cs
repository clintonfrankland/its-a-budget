using ClintonFrankland.Models;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Components.Pages;

public partial class Home
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private SiteInfoService SiteInfoService { get; set; } = default!;
    [Inject] private DashboardDataService DashboardData { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IOptions<AuthentikOidcOptions> AuthentikOptions { get; set; } = default!;

    private bool ShowWarning { get; set; }

    private LandingLoginModel LoginModel { get; } = new();

    private DashboardSnapshotViewModel? Snapshot { get; set; }
    private string? SnapshotError { get; set; }
    private bool _showAllCategories;
    private bool IsAuthentikEnabled => AuthentikOptions.Value.IsUsable;
    private string AuthentikLoginUrl => AuthentikLoginLinks.BuildLoginUrl(AuthentikOptions.Value, "/");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await AuthService.InitializeAsync();

        if (AuthService.IsAuthenticated)
        {
            await LoadSnapshotAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadSnapshotAsync()
    {
        SnapshotError = null;

        try
        {
            // Config-fallback login uses UserId=0, so use DefaultUserId for data.
            var userId = AuthService.CurrentUser.UserId > 0
                ? AuthService.CurrentUser.UserId
                : SiteInfoService.DefaultUserId;

            Snapshot = await DashboardData.GetSnapshotAsync(userId, DateTime.Today);
        }
        catch (Exception ex)
        {
            SnapshotError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task HandleLogin()
    {
        ShowWarning = false;

        if (await AuthService.ValidateCredentialsAsync(LoginModel.Username, LoginModel.Password))
        {
            // Stay on home page after login - refresh to show authenticated content
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }

        ShowWarning = true;
    }

    private sealed class LandingLoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
