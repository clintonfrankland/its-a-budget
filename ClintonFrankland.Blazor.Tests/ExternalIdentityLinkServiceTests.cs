using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class ExternalIdentityLinkServiceTests
{
    [Fact]
    public async Task LinkExternalIdentityAsync_StoresSubjectMappingAndLoginMetadata()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(1, "clinton"));
        await db.SaveChangesAsync();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 4, 20, 0, 0, TimeSpan.Zero));
        var service = new ExternalIdentityLinkService(db, timeProvider);

        var user = await service.LinkExternalIdentityAsync(
            1,
            new ExternalIdentityProfile("Authentik", "oidc-sub-123", " clinton@example.com ", " Clinton "));

        Assert.Equal("authentik", user.ExternalProvider);
        Assert.Equal("oidc-sub-123", user.ExternalSubject);
        Assert.Equal("clinton@example.com", user.ExternalEmail);
        Assert.Equal("Clinton", user.ExternalDisplayName);
        Assert.Equal(new DateTime(2026, 7, 4, 20, 0, 0, DateTimeKind.Utc), user.LastExternalLoginUtc);
    }

    [Fact]
    public async Task LinkExternalIdentityAsync_RejectsDuplicateSubjectForAnotherActiveUser()
    {
        await using var db = CreateDbContext();
        db.Users.AddRange(
            User(1, "clinton", "authentik", "same-subject"),
            User(2, "tara"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LinkExternalIdentityAsync(2, new ExternalIdentityProfile("Authentik", "same-subject")));

        Assert.Contains("already linked", ex.Message);
    }

    [Fact]
    public async Task LinkExternalIdentityAsync_AllowsSubjectReuseFromDeletedUser()
    {
        await using var db = CreateDbContext();
        db.Users.AddRange(
            User(1, "old", "authentik", "same-subject", isDeleted: true),
            User(2, "active"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var user = await service.LinkExternalIdentityAsync(2, new ExternalIdentityProfile("Authentik", "same-subject"));

        Assert.Equal(2, user.UserId);
        Assert.Equal("same-subject", user.ExternalSubject);
    }

    [Fact]
    public async Task LinkExternalIdentityAsAdminAsync_RejectsNonAdminActor()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(1, "clinton"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LinkExternalIdentityAsAdminAsync(
                CurrentUser(1, isAdmin: false),
                1,
                new ExternalIdentityProfile("authentik", "subject")));

        Assert.Contains("Only admins", ex.Message);
        Assert.Null(db.Users.Single().ExternalSubject);
    }

    [Fact]
    public async Task GetVisibleUsersAsync_ReturnsOnlySelfForRegularUsers()
    {
        await using var db = CreateDbContext();
        db.Users.AddRange(
            User(1, "clinton", "authentik", "subject-1"),
            User(2, "tara", "authentik", "subject-2"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var visibleUsers = await service.GetVisibleUsersAsync(CurrentUser(2, isAdmin: false));

        var user = Assert.Single(visibleUsers);
        Assert.Equal(2, user.UserId);
        Assert.Equal("subject-2", user.ExternalSubject);
    }

    [Fact]
    public async Task GetVisibleUsersAsync_ReturnsActiveUsersForAdminsOnly()
    {
        await using var db = CreateDbContext();
        db.Users.AddRange(
            User(1, "clinton"),
            User(2, "deleted", isDeleted: true));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var visibleUsers = await service.GetVisibleUsersAsync(CurrentUser(1, isAdmin: true));

        var user = Assert.Single(visibleUsers);
        Assert.Equal("clinton", user.UserName);
    }

    [Fact]
    public async Task LinkExternalIdentityAsAdminAsync_RelinksExistingIdentity()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(1, "clinton", "authentik", "old-subject"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var user = await service.LinkExternalIdentityAsAdminAsync(
            CurrentUser(99, isAdmin: true),
            1,
            new ExternalIdentityProfile("authentik", "new-subject", "new@example.com", "New Name"));

        Assert.Equal("new-subject", user.ExternalSubject);
        Assert.Equal("new@example.com", user.ExternalEmail);
        Assert.Equal("New Name", user.ExternalDisplayName);
    }

    [Fact]
    public async Task LinkExternalIdentityForCurrentUserAsync_LinksOnlySignedInUser()
    {
        await using var db = CreateDbContext();
        db.Users.AddRange(User(1, "clinton"), User(2, "tara"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var user = await service.LinkExternalIdentityForCurrentUserAsync(
            CurrentUser(2, isAdmin: false),
            new ExternalIdentityProfile("authentik", "self-subject", "tara@example.com", "Tara"));

        Assert.Equal(2, user.UserId);
        Assert.Equal("self-subject", user.ExternalSubject);
        Assert.Null(db.Users.Single(candidate => candidate.UserId == 1).ExternalSubject);
    }

    [Fact]
    public async Task LinkExternalIdentityForCurrentUserAsync_RejectsFallbackUserWithoutBudgetUserId()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LinkExternalIdentityForCurrentUserAsync(
                CurrentUser(0, isAdmin: true),
                new ExternalIdentityProfile("authentik", "subject")));

        Assert.Contains("Sign in with your Budget username", ex.Message);
    }

    [Fact]
    public async Task UnlinkExternalIdentityAsAdminAsync_ClearsLinkedIdentity()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(1, "clinton", "authentik", "subject"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var user = await service.UnlinkExternalIdentityAsAdminAsync(CurrentUser(99, isAdmin: true), 1);

        Assert.Null(user.ExternalProvider);
        Assert.Null(user.ExternalSubject);
        Assert.Null(user.ExternalEmail);
        Assert.Null(user.ExternalDisplayName);
        Assert.Null(user.LastExternalLoginUtc);
    }

    [Fact]
    public async Task UnlinkExternalIdentityForCurrentUserAsync_ClearsOnlySignedInUser()
    {
        await using var db = CreateDbContext();
        db.Users.AddRange(
            User(1, "clinton", "authentik", "subject-1"),
            User(2, "tara", "authentik", "subject-2"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var user = await service.UnlinkExternalIdentityForCurrentUserAsync(CurrentUser(2, isAdmin: false));

        Assert.Equal(2, user.UserId);
        Assert.Null(user.ExternalProvider);
        Assert.Null(user.ExternalSubject);
        Assert.Equal("subject-1", db.Users.Single(candidate => candidate.UserId == 1).ExternalSubject);
    }

    [Fact]
    public async Task UnlinkExternalIdentityAsAdminAsync_RejectsDeletedUsers()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(1, "deleted", "authentik", "subject", isDeleted: true));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UnlinkExternalIdentityAsAdminAsync(CurrentUser(99, isAdmin: true), 1));

        Assert.Contains("Active Budget user", ex.Message);
    }

    [Fact]
    public async Task FindLinkedUserAsync_IgnoresLegacyUsersWithNullExternalFields()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(1, "legacy"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var user = await service.FindLinkedUserAsync("authentik", "missing-subject");

        Assert.Null(user);
    }

    [Fact]
    public async Task RecordExternalLoginAsync_DoesNotLinkByMatchingEmail()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(1, "legacy", emailAddress: "clinton@example.com"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var user = await service.RecordExternalLoginAsync(
            new ExternalIdentityProfile("authentik", "new-subject", "clinton@example.com", "Clinton"));

        Assert.Null(user);
        Assert.Null(db.Users.Single().ExternalSubject);
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static ExternalIdentityLinkService CreateService(ClintonFranklandDbContext db) =>
        new(db, TimeProvider.System);

    private static UserInfo CurrentUser(int userId, bool isAdmin) => new()
    {
        UserId = userId,
        SiteId = 1,
        UserName = $"user-{userId}",
        DisplayName = $"User {userId}",
        IsAdmin = isAdmin,
        IsLoggedIn = true
    };

    private static User User(
        int userId,
        string userName,
        string? externalProvider = null,
        string? externalSubject = null,
        bool isDeleted = false,
        string? emailAddress = null) => new()
    {
        UserId = userId,
        SiteId = 1,
        UserName = userName,
        DisplayName = userName,
        EmailAddress = emailAddress,
        IsAdmin = false,
        ListButtonsRight = true,
        Salt = "salt",
        PasswordHash = "hash",
        IsDeleted = isDeleted,
        FirstLogin = DateTime.UtcNow,
        LastLogin = DateTime.UtcNow,
        ExternalProvider = externalProvider,
        ExternalSubject = externalSubject
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
