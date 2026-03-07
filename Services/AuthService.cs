using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class AuthService
{
    private const string AuthStorageKey = "auth_state";
    private readonly IConfiguration _configuration;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly ClintonFranklandDbContext _db;
    private UserInfo? _currentUser;
    private bool _isInitialized;

    public AuthService(IConfiguration configuration, ProtectedSessionStorage sessionStorage, ClintonFranklandDbContext db)
    {
        _configuration = configuration;
        _sessionStorage = sessionStorage;
        _db = db;
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

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            !u.IsDeleted &&
            u.UserName.ToLower() == username.Trim().ToLower());

        if (user is not null)
        {
            if (!PasswordUtility.VerifyPassword(password, user.Salt, user.PasswordHash))
                return false;

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LoginAsync(user);
            return true;
        }

        // Fallback to appsettings credentials for compatibility
        var validUser = _configuration["AppSettings:LoginUser"];
        var validPassword = _configuration["AppSettings:LoginPassword"];
        if (string.Equals(username, validUser, StringComparison.OrdinalIgnoreCase) && password == validPassword)
        {
            await LoginAsync(username);
            return true;
        }

        return false;
    }

    public async Task LoginAsync(User user)
    {
        _currentUser = new UserInfo
        {
            UserId = user.UserId,
            SiteId = user.SiteId,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            EmailAddress = user.EmailAddress,
            IsAdmin = user.IsAdmin,
            ListButtonsRight = user.ListButtonsRight,
            IsLoggedIn = true
        };

        await _sessionStorage.SetAsync(AuthStorageKey, _currentUser);
    }

    public async Task LoginAsync(string username)
    {
        _currentUser = new UserInfo
        {
            UserId = 0,
            SiteId = 0,
            UserName = username,
            DisplayName = username,
            EmailAddress = null,
            IsAdmin = true,
            ListButtonsRight = true,
            IsLoggedIn = true
        };

        await _sessionStorage.SetAsync(AuthStorageKey, _currentUser);
    }

    public async Task RefreshCurrentUserAsync(User user)
    {
        if (_currentUser is null || !_currentUser.IsLoggedIn) return;

        _currentUser.UserId = user.UserId;
        _currentUser.SiteId = user.SiteId;
        _currentUser.UserName = user.UserName;
        _currentUser.DisplayName = user.DisplayName;
        _currentUser.EmailAddress = user.EmailAddress;
        _currentUser.IsAdmin = user.IsAdmin;
        _currentUser.ListButtonsRight = user.ListButtonsRight;

        await _sessionStorage.SetAsync(AuthStorageKey, _currentUser);
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;
        await _sessionStorage.DeleteAsync(AuthStorageKey);
    }
}
