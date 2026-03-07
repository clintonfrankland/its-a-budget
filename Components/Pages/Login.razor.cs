using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class Login
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private LoginModel loginModel = new();
    private bool showWarning = false;

    [SupplyParameterFromQuery]
    public string? Return { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AuthService.InitializeAsync();
            if (AuthService.IsAuthenticated)
            {
                Navigation.NavigateTo(Return ?? "/");
            }
            StateHasChanged();
        }
    }

    private async Task HandleLogin()
    {
        if (await AuthService.ValidateCredentialsAsync(loginModel.Username, loginModel.Password))
        {
            Navigation.NavigateTo(Return ?? "/");
        }
        else
        {
            showWarning = true;
        }
    }

    private class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
