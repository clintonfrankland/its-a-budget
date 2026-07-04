using System.Security.Claims;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ClintonFrankland.Blazor.Tests;

public class AuthentikOidcTests
{
    [Fact]
    public void IsUsable_IsFalseWhenDisabledOrRequiredSettingsAreMissing()
    {
        var disabled = new AuthentikOidcOptions
        {
            Enabled = false,
            Authority = "https://auth.example.com/application/o/budget-app/",
            ClientId = "client",
            ClientSecret = "secret"
        };

        var missingSecret = new AuthentikOidcOptions
        {
            Enabled = true,
            Authority = "https://auth.example.com/application/o/budget-app/",
            ClientId = "client"
        };

        Assert.False(disabled.IsUsable);
        Assert.False(missingSecret.IsUsable);
    }

    [Fact]
    public void IsUsable_IsTrueWhenEnabledAndRequiredSettingsArePresent()
    {
        var options = new AuthentikOidcOptions
        {
            Enabled = true,
            Authority = "https://auth.example.com/application/o/budget-app/",
            ClientId = "client",
            ClientSecret = "secret"
        };

        Assert.True(options.IsUsable);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/checkbook?month=2026-07", "/checkbook?month=2026-07")]
    [InlineData("settings", "/")]
    [InlineData("https://evil.example.com/", "/")]
    [InlineData("//evil.example.com/", "/")]
    [InlineData("/\\evil", "/")]
    public void GetSafeLocalPath_AllowsOnlyRootedRelativeUrls(string? returnUrl, string expected)
    {
        Assert.Equal(expected, ReturnUrlUtility.GetSafeLocalPath(returnUrl));
    }

    [Fact]
    public void ChallengeLogin_ReturnsNotFoundWhenAuthentikIsDisabled()
    {
        var result = AuthentikOidcEndpoints.ChallengeLogin(new AuthentikOidcOptions(), "/checkbook");

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public void ChallengeLogin_UsesSafeReturnUrlAndOidcSchemeWhenEnabled()
    {
        var options = new AuthentikOidcOptions
        {
            Enabled = true,
            Authority = "https://auth.example.com/application/o/budget-app/",
            ClientId = "client",
            ClientSecret = "secret"
        };

        var result = Assert.IsType<ChallengeHttpResult>(
            AuthentikOidcEndpoints.ChallengeLogin(options, "https://evil.example.com/"));

        Assert.Equal("/", result.Properties?.RedirectUri);
        Assert.Contains(AuthentikOidcDefaults.OpenIdConnectScheme, result.AuthenticationSchemes);
    }

    [Fact]
    public void BuildExternalProfile_UsesStableSubjectAndProviderMetadata()
    {
        var principal = Principal(
            new Claim("sub", "authentik-subject"),
            new Claim("email", "clinton@example.com"),
            new Claim("name", "Clinton"));

        var profile = AuthentikOidcClaims.BuildExternalProfile(principal, "authentik");

        Assert.NotNull(profile);
        Assert.Equal("authentik", profile.Provider);
        Assert.Equal("authentik-subject", profile.Subject);
        Assert.Equal("clinton@example.com", profile.Email);
        Assert.Equal("Clinton", profile.DisplayName);
    }

    [Fact]
    public void BuildExternalProfile_ReturnsNullWhenSubjectIsMissing()
    {
        var principal = Principal(new Claim("email", "clinton@example.com"));

        Assert.Null(AuthentikOidcClaims.BuildExternalProfile(principal, "authentik"));
    }

    [Fact]
    public void IsInAllowedGroup_ParsesJsonArrayGroupClaims()
    {
        var principal = Principal(new Claim("groups", "[\"budget-users\",\"other\"]"));

        Assert.True(AuthentikOidcClaims.IsInAllowedGroup(principal, ["budget-users"]));
        Assert.False(AuthentikOidcClaims.IsInAllowedGroup(principal, ["admins"]));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}
