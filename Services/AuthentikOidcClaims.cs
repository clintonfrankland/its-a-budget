using System.Security.Claims;
using System.Text.Json;
using ClintonFrankland.Models.Entities;

namespace ClintonFrankland.Services;

public static class AuthentikOidcClaims
{
    public static ExternalIdentityProfile? BuildExternalProfile(ClaimsPrincipal principal, string providerName)
    {
        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        var displayName =
            principal.FindFirstValue("name") ??
            principal.FindFirstValue("preferred_username") ??
            principal.FindFirstValue(ClaimTypes.Name);

        return new ExternalIdentityProfile(providerName, subject, email, displayName);
    }

    public static IReadOnlySet<string> GetGroups(ClaimsPrincipal principal)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.Claims)
        {
            if (!IsGroupClaim(claim.Type))
                continue;

            AddGroupValues(groups, claim.Value);
        }

        return groups;
    }

    public static bool IsInAllowedGroup(ClaimsPrincipal principal, IEnumerable<string> allowedGroups)
    {
        var normalizedAllowedGroups = allowedGroups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedAllowedGroups.Count == 0)
            return true;

        var userGroups = GetGroups(principal);
        return userGroups.Any(normalizedAllowedGroups.Contains);
    }

    public static ClaimsPrincipal CreateLinkIntentPrincipal(
        ExternalIdentityProfile profile,
        IEnumerable<string> groups)
    {
        var claims = new List<Claim>
        {
            new(AuthentikOidcDefaults.LinkIntentClaim, "true"),
            new(AuthentikOidcDefaults.ExternalProviderClaim, profile.Provider),
            new(AuthentikOidcDefaults.ExternalSubjectClaim, profile.Subject)
        };

        if (!string.IsNullOrWhiteSpace(profile.Email))
            claims.Add(new Claim(AuthentikOidcDefaults.ExternalEmailClaim, profile.Email));
        if (!string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            claims.Add(new Claim(AuthentikOidcDefaults.ExternalDisplayNameClaim, profile.DisplayName));
            claims.Add(new Claim(ClaimTypes.Name, profile.DisplayName));
        }

        AddBudgetGroupClaims(claims, groups);
        return CreatePrincipal(claims);
    }

    public static ClaimsPrincipal CreateBudgetUserPrincipal(
        User user,
        IEnumerable<string> groups)
    {
        var claims = new List<Claim>
        {
            new(AuthentikOidcDefaults.BudgetUserIdClaim, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.DisplayName ?? user.UserName)
        };

        if (!string.IsNullOrWhiteSpace(user.EmailAddress))
            claims.Add(new Claim(ClaimTypes.Email, user.EmailAddress));
        if (user.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        AddBudgetGroupClaims(claims, groups);
        return CreatePrincipal(claims);
    }

    private static bool IsGroupClaim(string claimType) =>
        string.Equals(claimType, "groups", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "group", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "roles", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase);

    private static void AddGroupValues(HashSet<string> groups, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var trimmed = value.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                var values = JsonSerializer.Deserialize<string[]>(trimmed);
                if (values is not null)
                {
                    foreach (var item in values)
                        AddGroupValues(groups, item);
                }

                return;
            }
            catch (JsonException)
            {
                // Fall through and keep the raw value.
            }
        }

        groups.Add(trimmed);
    }

    private static void AddBudgetGroupClaims(List<Claim> claims, IEnumerable<string> groups)
    {
        foreach (var group in groups.Where(group => !string.IsNullOrWhiteSpace(group)).Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(AuthentikOidcDefaults.BudgetGroupClaim, group.Trim()));
    }

    private static ClaimsPrincipal CreatePrincipal(IEnumerable<Claim> claims) =>
        new(new ClaimsIdentity(
            claims,
            AuthentikOidcDefaults.OpenIdConnectScheme,
            ClaimTypes.Name,
            ClaimTypes.Role));
}
