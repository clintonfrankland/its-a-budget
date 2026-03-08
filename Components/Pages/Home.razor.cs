using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class Home
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool ShowWarning { get; set; }

    private LandingLoginModel LoginModel { get; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await AuthService.InitializeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleLogin()
    {
        ShowWarning = false;

        if (await AuthService.ValidateCredentialsAsync(LoginModel.Username, LoginModel.Password))
        {
            // Per requirements: landing-page login always goes to Checkbook for now.
            Navigation.NavigateTo("/checkbook");
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
