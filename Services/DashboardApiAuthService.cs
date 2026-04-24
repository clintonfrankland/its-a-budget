using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed class DashboardApiAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ClintonFranklandDbContext _db;
    private readonly SiteInfoService _siteInfo;

    public DashboardApiAuthService(
        IConfiguration configuration,
        ClintonFranklandDbContext db,
        SiteInfoService siteInfo)
    {
        _configuration = configuration;
        _db = db;
        _siteInfo = siteInfo;
    }

    public async Task<int?> ValidateAndResolveUserIdAsync(string username, string password)
    {
        var normalizedUserName = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            !u.IsDeleted &&
            u.UserName.ToLower() == normalizedUserName.ToLower());

        if (user is not null && PasswordUtility.VerifyPassword(password, user.Salt, user.PasswordHash))
            return user.UserId;

        // Keep compatibility with the appsettings fallback login model used by the UI.
        var validUser = _configuration["AppSettings:LoginUser"];
        var validPassword = _configuration["AppSettings:LoginPassword"];
        if (string.Equals(normalizedUserName, validUser, StringComparison.OrdinalIgnoreCase) && password == validPassword)
            return _siteInfo.DefaultUserId;

        return null;
    }
}
