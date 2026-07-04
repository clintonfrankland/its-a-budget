using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;

namespace ClintonFrankland.Services;

public sealed class AuthentikOidcOptions
{
    public const string SectionName = "Authentication:Authentik";

    public bool Enabled { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ProviderName { get; set; } = "authentik";
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
    public string CookieName { get; set; } = "BudgetApp.Authentik";
    public List<string> AllowedGroups { get; set; } = [];

    public bool IsUsable =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Authority) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);

    public string NormalizedProviderName =>
        string.IsNullOrWhiteSpace(ProviderName) ? "authentik" : ProviderName.Trim().ToLowerInvariant();
}

public static class AuthentikOidcDefaults
{
    public const string CookieScheme = "BudgetApp.Authentik.Cookie";
    public const string OpenIdConnectScheme = OpenIdConnectDefaults.AuthenticationScheme;
    public const string BudgetUserIdClaim = "budget:user_id";
    public const string BudgetGroupClaim = "budget:authentik_group";
}

public static class ReturnUrlUtility
{
    public static string GetSafeLocalPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if (!Uri.TryCreate(returnUrl, UriKind.Relative, out var uri))
            return "/";

        var path = uri.ToString();
        if (!path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal) ||
            path.StartsWith("/\\", StringComparison.Ordinal) ||
            path.Contains("\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return path;
    }
}

public static class AuthentikOidcEndpoints
{
    public static IResult ChallengeLogin(AuthentikOidcOptions options, string? returnUrl)
    {
        if (!options.IsUsable)
            return Results.NotFound();

        var safeReturnUrl = ReturnUrlUtility.GetSafeLocalPath(returnUrl);
        return Results.Challenge(
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                RedirectUri = safeReturnUrl
            },
            [AuthentikOidcDefaults.OpenIdConnectScheme]);
    }

    public static IResult LocalLogout(string? returnUrl)
    {
        var safeReturnUrl = ReturnUrlUtility.GetSafeLocalPath(returnUrl);
        return Results.Redirect(safeReturnUrl);
    }
}
