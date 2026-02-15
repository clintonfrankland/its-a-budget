using ClintonFrankland.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ClintonFrankland.Services;

public class AuthService
{
    private const string AuthStorageKey = "auth_state";
    private readonly IConfiguration _configuration;
    private readonly ProtectedSessionStorage _sessionStorage;
    private UserInfo? _currentUser;
    private bool _isInitialized;

    public AuthService(IConfiguration configuration, ProtectedSessionStorage sessionStorage)
    {
        _configuration = configuration;
        _sessionStorage = sessionStorage;
    }

    public UserInfo CurrentUser => _currentUser ?? new UserInfo();

    public bool IsAuthenticated => _currentUser?.IsLoggedIn ?? false;

    public bool IsInitialized => _isInitialized;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            var result = await _sessionStorage.GetAsync<UserInfo>(AuthStorageKey);
            if (result.Success && result.Value != null)
            {
                _currentUser = result.Value;
            }
        }
        catch
        {
            // Session storage not available during prerendering
        }

        _isInitialized = true;
    }

    public bool ValidateCredentials(string username, string password)
    {
        var validUser = _configuration["AppSettings:LoginUser"];
        var validPassword = _configuration["AppSettings:LoginPassword"];

        return string.Equals(username, validUser, StringComparison.OrdinalIgnoreCase) 
               && password == validPassword;
    }

    public async Task LoginAsync(string username)
    {
        _currentUser = new UserInfo
        {
            UserName = username,
            DisplayName = "Clinton Frankland",
            IsLoggedIn = true
        };

        await _sessionStorage.SetAsync(AuthStorageKey, _currentUser);
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;
        await _sessionStorage.DeleteAsync(AuthStorageKey);
    }
}
