using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Layout;

public partial class MainLayout
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private SiteInfoService SiteInfoService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private bool _isLoading = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AuthService.InitializeAsync();
            _isLoading = false;
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

    private string GetLoginText()
    {
        return AuthService.IsAuthenticated ? AuthService.CurrentUser.DisplayName : "Login";
    }

    private string GetLoginLink()
    {
        return AuthService.IsAuthenticated ? "/" : "/login";
    }

    private async Task HandleLoginClick()
    {
        if (AuthService.IsAuthenticated)
        {
            await AuthService.LogoutAsync();
            Navigation.NavigateTo("/", forceLoad: true);
        }
    }
}
