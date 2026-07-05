using ClintonFrankland.Models;
using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public class CurrentUserContextTests
{
    [Fact]
    public void ResolveEffectiveBudgetUserId_UsesAuthenticatedBudgetUser()
    {
        var currentUser = new UserInfo
        {
            UserId = 42,
            IsLoggedIn = true
        };

        var userId = CurrentUserContext.ResolveEffectiveBudgetUserId(currentUser, fallbackDefaultUserId: 1);

        Assert.Equal(42, userId);
    }

    [Fact]
    public void ResolveEffectiveBudgetUserId_UsesDefaultUserOnlyForConfigFallbackLogin()
    {
        var currentUser = new UserInfo
        {
            UserId = 0,
            IsLoggedIn = true
        };

        var userId = CurrentUserContext.ResolveEffectiveBudgetUserId(currentUser, fallbackDefaultUserId: 1);

        Assert.Equal(1, userId);
    }

    [Fact]
    public void ResolveEffectiveBudgetUserId_RejectsUnauthenticatedUsers()
    {
        var currentUser = new UserInfo
        {
            UserId = 42,
            IsLoggedIn = false
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CurrentUserContext.ResolveEffectiveBudgetUserId(currentUser, fallbackDefaultUserId: 1));

        Assert.Equal("The current user must be authenticated before Budget data can be resolved.", ex.Message);
    }

    [Fact]
    public void ResolveEffectiveBudgetUserId_RejectsInvalidConfigFallbackDefault()
    {
        var currentUser = new UserInfo
        {
            UserId = 0,
            IsLoggedIn = true
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CurrentUserContext.ResolveEffectiveBudgetUserId(currentUser, fallbackDefaultUserId: 0));

        Assert.Equal("AppSettings:DefaultUserId must be configured for config-fallback login.", ex.Message);
    }
}
