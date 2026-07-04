using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Components.Pages;

public partial class Login
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IOptions<AuthentikOidcOptions> AuthentikOptions { get; set; } = default!;

    private LoginModel loginModel = new();
    private bool showWarning = false;
    private string? externalError;

    [SupplyParameterFromQuery]
    public string? Return { get; set; }

    [SupplyParameterFromQuery]
    public string? ExternalError { get; set; }

    private bool IsAuthentikEnabled => AuthentikOptions.Value.IsUsable;

    private string SafeReturnUrl => ReturnUrlUtility.GetSafeLocalPath(Return);

    private string AuthentikLoginUrl => $"/auth/authentik/login?returnUrl={Uri.EscapeDataString(SafeReturnUrl)}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            externalError = ExternalError;
            await AuthService.InitializeAsync();
            if (AuthService.IsAuthenticated)
            {
                Navigation.NavigateTo(SafeReturnUrl);
            }
            StateHasChanged();
        }
    }

    private async Task HandleLogin()
    {
        if (await AuthService.ValidateCredentialsAsync(loginModel.Username, loginModel.Password))
        {
            Navigation.NavigateTo(SafeReturnUrl);
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
