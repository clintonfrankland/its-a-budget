using System.Security.Claims;
using System.Text.Json;

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
}
