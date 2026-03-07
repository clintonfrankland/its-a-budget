using System.Collections.Concurrent;
using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class AuthService
{
    private sealed class FailedAttemptState
    {
        public List<DateTime> FailedAttemptsUtc { get; } = new();
        public DateTime? LockedOutUntilUtc { get; set; }
    }

    private const string AuthStorageKey = "auth_state";
    private static readonly ConcurrentDictionary<string, FailedAttemptState> FailedLoginState = new();

    private readonly IConfiguration _configuration;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly ClintonFranklandDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private UserInfo? _currentUser;
    private bool _isInitialized;

    public AuthService(
        IConfiguration configuration,
        ProtectedSessionStorage sessionStorage,
        ClintonFranklandDbContext db,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _sessionStorage = sessionStorage;
        _db = db;
        _httpContextAccessor = httpContextAccessor;
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
        var normalizedUserName = (username ?? string.Empty).Trim();
        var key = BuildAttemptKey(normalizedUserName);

        if (IsLockedOut(key, out var lockoutReason))
        {
            await WriteAuditAsync(normalizedUserName, false, lockoutReason);
            return false;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            !u.IsDeleted &&
            u.UserName.ToLower() == normalizedUserName.ToLower());

        if (user is not null)
        {
            if (!PasswordUtility.VerifyPassword(password, user.Salt, user.PasswordHash))
            {
                RecordFailedAttempt(key);
                await WriteAuditAsync(normalizedUserName, false, "Invalid username or password");
                return false;
            }

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            ClearFailedAttempts(key);
            await LoginAsync(user);
            await WriteAuditAsync(normalizedUserName, true, "DB user login success");
            return true;
        }

        // Fallback to appsettings credentials for compatibility.
        // Intentionally allowed even if DB users exist.
        var validUser = _configuration["AppSettings:LoginUser"];
        var validPassword = _configuration["AppSettings:LoginPassword"];
        if (string.Equals(normalizedUserName, validUser, StringComparison.OrdinalIgnoreCase) && password == validPassword)
        {
            ClearFailedAttempts(key);
            await LoginAsync(normalizedUserName);
            await WriteAuditAsync(normalizedUserName, true, "Config credential login success");
            return true;
        }

        RecordFailedAttempt(key);
        await WriteAuditAsync(normalizedUserName, false, "Invalid username or password");
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

    private int MaxFailedAttempts => Math.Max(1, _configuration.GetValue<int?>("AuthSecurity:MaxFailedAttempts") ?? 5);
    private int FailedAttemptWindowMinutes => Math.Max(1, _configuration.GetValue<int?>("AuthSecurity:FailedAttemptWindowMinutes") ?? 15);
    private int LockoutMinutes => Math.Max(1, _configuration.GetValue<int?>("AuthSecurity:LockoutMinutes") ?? 15);

    private string BuildAttemptKey(string userName)
    {
        var ip = GetClientIp();
        return $"{userName.ToLowerInvariant()}|{ip}";
    }

    private bool IsLockedOut(string key, out string reason)
    {
        reason = string.Empty;

        if (!FailedLoginState.TryGetValue(key, out var state))
            return false;

        lock (state)
        {
            if (state.LockedOutUntilUtc is null)
                return false;

            if (DateTime.UtcNow >= state.LockedOutUntilUtc.Value)
            {
                state.LockedOutUntilUtc = null;
                state.FailedAttemptsUtc.Clear();
                return false;
            }

            var mins = Math.Ceiling((state.LockedOutUntilUtc.Value - DateTime.UtcNow).TotalMinutes);
            reason = $"Account temporarily locked. Try again in {Math.Max(1, (int)mins)} minute(s).";
            return true;
        }
    }

    private void RecordFailedAttempt(string key)
    {
        var now = DateTime.UtcNow;
        var state = FailedLoginState.GetOrAdd(key, _ => new FailedAttemptState());

        lock (state)
        {
            var windowStart = now.AddMinutes(-FailedAttemptWindowMinutes);
            state.FailedAttemptsUtc.RemoveAll(x => x < windowStart);
            state.FailedAttemptsUtc.Add(now);

            if (state.FailedAttemptsUtc.Count >= MaxFailedAttempts)
            {
                state.LockedOutUntilUtc = now.AddMinutes(LockoutMinutes);
                state.FailedAttemptsUtc.Clear();
            }
        }
    }

    private void ClearFailedAttempts(string key)
    {
        FailedLoginState.TryRemove(key, out _);
    }

    private string GetClientIp()
    {
        var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
    }

    private async Task WriteAuditAsync(string userName, bool succeeded, string reason)
    {
        try
        {
            _db.AuthLoginAudits.Add(new AuthLoginAudit
            {
                AttemptedAtUtc = DateTime.UtcNow,
                UserName = string.IsNullOrWhiteSpace(userName) ? null : userName,
                ClientIp = GetClientIp(),
                Succeeded = succeeded,
                Reason = reason
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Don't block authentication flow because audit persistence failed.
        }
    }
}
