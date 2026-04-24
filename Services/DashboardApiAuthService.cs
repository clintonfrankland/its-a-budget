namespace ClintonFrankland.Services;

public sealed class DashboardApiAuthService
{
    private readonly AuthService _authService;
    private readonly SiteInfoService _siteInfo;

    public DashboardApiAuthService(AuthService authService, SiteInfoService siteInfo)
    {
        _authService = authService;
        _siteInfo = siteInfo;
    }

    public Task<int?> ValidateAndResolveUserIdAsync(string username, string password)
    {
        return _authService.ValidateApiCredentialsAsync(username, password, _siteInfo.DefaultUserId);
    }
}
