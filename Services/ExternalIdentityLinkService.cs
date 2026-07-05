using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed record ExternalIdentityProfile(
    string Provider,
    string Subject,
    string? Email = null,
    string? DisplayName = null);

public sealed class ExternalIdentityLinkService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ExternalIdentityLinkService(ClintonFranklandDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<User?> FindLinkedUserAsync(string provider, string subject)
    {
        var normalizedProvider = NormalizeRequired(provider, nameof(provider));
        var normalizedSubject = NormalizeRequired(subject, nameof(subject));

        return await _db.Users.FirstOrDefaultAsync(user =>
            !user.IsDeleted &&
            user.ExternalProvider == normalizedProvider &&
            user.ExternalSubject == normalizedSubject);
    }

    public async Task<List<User>> GetVisibleUsersAsync(UserInfo currentUser)
    {
        var query = _db.Users.AsNoTracking().Where(user => !user.IsDeleted);

        if (!currentUser.IsAdmin)
            query = query.Where(user => user.UserId == currentUser.UserId);

        return await query.OrderBy(user => user.UserId).ToListAsync();
    }

    public Task<User> LinkExternalIdentityAsAdminAsync(
        UserInfo actor,
        int userId,
        ExternalIdentityProfile profile)
    {
        if (!actor.IsAdmin)
            throw new UnauthorizedAccessException("Only admins can link Authentik identities.");

        return LinkExternalIdentityAsync(userId, profile);
    }

    public Task<User> LinkExternalIdentityForCurrentUserAsync(UserInfo actor, ExternalIdentityProfile profile)
    {
        if (!actor.IsLoggedIn || actor.UserId <= 0)
            throw new UnauthorizedAccessException("Sign in with your Budget username and password before linking Authentik.");

        return LinkExternalIdentityAsync(actor.UserId, profile);
    }

    public async Task<User> LinkExternalIdentityAsync(int userId, ExternalIdentityProfile profile)
    {
        var normalizedProvider = NormalizeRequired(profile.Provider, nameof(profile.Provider));
        var normalizedSubject = NormalizeRequired(profile.Subject, nameof(profile.Subject));

        var duplicate = await _db.Users.FirstOrDefaultAsync(user =>
            !user.IsDeleted &&
            user.UserId != userId &&
            user.ExternalProvider == normalizedProvider &&
            user.ExternalSubject == normalizedSubject);

        if (duplicate is not null)
            throw new InvalidOperationException("External identity is already linked to another active Budget user.");

        var user = await _db.Users.FirstOrDefaultAsync(user => user.UserId == userId && !user.IsDeleted)
            ?? throw new InvalidOperationException("Active Budget user was not found.");

        ApplyExternalIdentity(user, normalizedProvider, normalizedSubject, profile);
        await _db.SaveChangesAsync();

        return user;
    }

    public async Task<User?> RecordExternalLoginAsync(ExternalIdentityProfile profile)
    {
        var user = await FindLinkedUserAsync(profile.Provider, profile.Subject);
        if (user is null)
            return null;

        ApplyExternalIdentity(user, user.ExternalProvider!, user.ExternalSubject!, profile);
        await _db.SaveChangesAsync();

        return user;
    }

    public async Task<User> UnlinkExternalIdentityAsAdminAsync(UserInfo actor, int userId)
    {
        if (!actor.IsAdmin)
            throw new UnauthorizedAccessException("Only admins can unlink Authentik identities.");

        var user = await _db.Users.FirstOrDefaultAsync(user => user.UserId == userId && !user.IsDeleted)
            ?? throw new InvalidOperationException("Active Budget user was not found.");

        user.ExternalProvider = null;
        user.ExternalSubject = null;
        user.ExternalEmail = null;
        user.ExternalDisplayName = null;
        user.LastExternalLoginUtc = null;

        await _db.SaveChangesAsync();
        return user;
    }

    public Task<User> UnlinkExternalIdentityForCurrentUserAsync(UserInfo actor)
    {
        if (!actor.IsLoggedIn || actor.UserId <= 0)
            throw new UnauthorizedAccessException("Sign in with your Budget username and password before unlinking Authentik.");

        return UnlinkExternalIdentityAsync(actor.UserId);
    }

    private async Task<User> UnlinkExternalIdentityAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(user => user.UserId == userId && !user.IsDeleted)
            ?? throw new InvalidOperationException("Active Budget user was not found.");

        user.ExternalProvider = null;
        user.ExternalSubject = null;
        user.ExternalEmail = null;
        user.ExternalDisplayName = null;
        user.LastExternalLoginUtc = null;

        await _db.SaveChangesAsync();
        return user;
    }

    public static bool HasExternalIdentity(User user) =>
        !string.IsNullOrWhiteSpace(user.ExternalProvider) &&
        !string.IsNullOrWhiteSpace(user.ExternalSubject);

    private void ApplyExternalIdentity(
        User user,
        string normalizedProvider,
        string normalizedSubject,
        ExternalIdentityProfile profile)
    {
        user.ExternalProvider = normalizedProvider;
        user.ExternalSubject = normalizedSubject;
        user.ExternalEmail = NormalizeOptional(profile.Email);
        user.ExternalDisplayName = NormalizeOptional(profile.DisplayName);
        user.LastExternalLoginUtc = _timeProvider.GetUtcNow().UtcDateTime;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("External identity provider and subject are required.", parameterName);

        return parameterName.Contains("Provider", StringComparison.OrdinalIgnoreCase)
            ? normalized.ToLowerInvariant()
            : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
