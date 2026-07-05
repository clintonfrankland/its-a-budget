using ClintonFrankland.Models;

namespace ClintonFrankland.Services;

public class CurrentUserContext
{
    private readonly AuthService _authService;
    private readonly SiteInfoService _siteInfo;

    public CurrentUserContext(AuthService authService, SiteInfoService siteInfo)
    {
        _authService = authService;
        _siteInfo = siteInfo;
    }

    public int UserId => ResolveEffectiveBudgetUserId(_authService.CurrentUser, _siteInfo.DefaultUserId);

    public bool IsConfigFallbackLogin =>
        _authService.CurrentUser.IsLoggedIn && _authService.CurrentUser.UserId <= 0;

    public static int ResolveEffectiveBudgetUserId(UserInfo currentUser, int fallbackDefaultUserId)
    {
        if (!currentUser.IsLoggedIn)
            throw new InvalidOperationException("The current user must be authenticated before Budget data can be resolved.");

        if (currentUser.UserId > 0)
            return currentUser.UserId;

        if (fallbackDefaultUserId <= 0)
            throw new InvalidOperationException("AppSettings:DefaultUserId must be configured for config-fallback login.");

        // The legacy AppSettings fallback login has no cfUsers row, so it explicitly reads the configured default user.
        return fallbackDefaultUserId;
    }
}
