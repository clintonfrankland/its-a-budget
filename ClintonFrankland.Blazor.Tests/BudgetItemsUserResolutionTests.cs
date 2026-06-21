using ClintonFrankland.Components.Pages;
using ClintonFrankland.Models;

namespace ClintonFrankland.Blazor.Tests;

public class BudgetItemsUserResolutionTests
{
    [Fact]
    public void ResolveBudgetItemsUserId_UsesDbBackedCurrentUser()
    {
        var currentUser = new UserInfo
        {
            UserId = 42,
            IsLoggedIn = true
        };

        var userId = BudgetItems.ResolveBudgetItemsUserId(currentUser, fallbackDefaultUserId: 1);

        Assert.Equal(42, userId);
    }

    [Fact]
    public void ResolveBudgetItemsUserId_UsesDefaultUserForConfigFallbackLogin()
    {
        var currentUser = new UserInfo
        {
            UserId = 0,
            IsLoggedIn = true
        };

        var userId = BudgetItems.ResolveBudgetItemsUserId(currentUser, fallbackDefaultUserId: 1);

        Assert.Equal(1, userId);
    }
}
