using System.Security.Claims;
using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
    public void BuildLoginUrl_ReturnsEmptyWhenAuthentikIsDisabled()
    {
        var result = AuthentikLoginLinks.BuildLoginUrl(new AuthentikOidcOptions(), "/");

        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("/", "/auth/authentik/login?returnUrl=%2F")]
    [InlineData("/checkbook?month=2026-07", "/auth/authentik/login?returnUrl=%2Fcheckbook%3Fmonth%3D2026-07")]
    [InlineData("https://evil.example.com/", "/auth/authentik/login?returnUrl=%2F")]
    public void BuildLoginUrl_UsesSafeReturnUrlWhenEnabled(string? returnUrl, string expected)
    {
        var options = new AuthentikOidcOptions
        {
            Enabled = true,
            Authority = "https://auth.example.com/application/o/budget/",
            ClientId = "client",
            ClientSecret = "secret"
        };

        var result = AuthentikLoginLinks.BuildLoginUrl(options, returnUrl);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/checkbook", "/checkbook")]
    [InlineData("https://evil.example.com/signout", "/")]
    [InlineData("//evil.example.com/signout", "/")]
    public void LocalLogout_UsesSafeReturnUrl(string? returnUrl, string expectedRedirect)
    {
        var result = Assert.IsType<RedirectHttpResult>(AuthentikOidcEndpoints.LocalLogout(returnUrl));

        Assert.Equal(expectedRedirect, result.Url);
    }

    [Fact]
    public async Task ValidateApiCredentialsAsync_AllowsDbUserLoginWhenAuthentikIsEnabled()
    {
        await using var db = CreateDbContext();
        var salt = PasswordUtility.CreateSalt();
        db.Users.Add(User(7, "clinton", salt, PasswordUtility.HashPassword("db-password", salt)));
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var userId = await service.ValidateApiCredentialsAsync("clinton", "db-password", fallbackUserId: 3);

        Assert.Equal(7, userId);
    }

    [Fact]
    public async Task ValidateApiCredentialsAsync_AllowsConfigFallbackWhenAuthentikIsEnabledAndDbUserExists()
    {
        await using var db = CreateDbContext();
        var salt = PasswordUtility.CreateSalt();
        db.Users.Add(User(7, "breakglass", salt, PasswordUtility.HashPassword("db-password", salt)));
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var userId = await service.ValidateApiCredentialsAsync("breakglass", "config-password", fallbackUserId: 3);

        Assert.Equal(3, userId);
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

    [Fact]
    public void Fingerprint_ReturnsStableShortHashWithoutRawSubject()
    {
        var result = AuthentikOidcDiagnostics.Fingerprint("authentik-subject");

        Assert.Equal("d2c98aa947ac", result);
        Assert.DoesNotContain("authentik", result);
        Assert.DoesNotContain("subject", result);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static AuthService CreateAuthService(ClintonFranklandDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:LoginUser"] = "breakglass",
                ["AppSettings:LoginPassword"] = "config-password",
                ["Authentication:Authentik:Enabled"] = "true",
                ["Authentication:Authentik:Authority"] = "https://auth.example.com/application/o/budget-app/",
                ["Authentication:Authentik:ClientId"] = "client",
                ["Authentication:Authentik:ClientSecret"] = "secret"
            })
            .Build();

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        return new AuthService(configuration, sessionStorage: null!, db, httpContextAccessor);
    }

    private static User User(int userId, string userName, string salt, string passwordHash) => new()
    {
        UserId = userId,
        SiteId = 1,
        UserName = userName,
        DisplayName = userName,
        IsAdmin = false,
        ListButtonsRight = true,
        Salt = salt,
        PasswordHash = passwordHash,
        IsDeleted = false,
        FirstLogin = DateTime.UtcNow,
        LastLogin = DateTime.UtcNow
    };
}
