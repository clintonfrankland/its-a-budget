using System.Reflection;
using Bunit;
using ClintonFrankland.Components.Pages;
using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClintonFrankland.Blazor.Tests;

public sealed class SharingPageTests
{
    [Fact]
    public void SharingPage_OwnerCanManageMembersInvitesAndTransferOwnership()
    {
        using var context = CreateSharingContext(BudgetMemberRole.Owner, includePeerMember: true);

        var page = context.Render<Sharing>();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Owner access - 2 active members", page.Markup);
            Assert.Contains("Create Invite", page.Markup);
            Assert.Contains("Members", page.Markup);
            Assert.Contains("Pending Invites", page.Markup);
            Assert.Contains("Update", page.Markup);
            Assert.Contains("Remove", page.Markup);
            Assert.Contains("Make Owner", page.Markup);
            Assert.DoesNotContain("Leave Budget", page.Markup);
            Assert.DoesNotContain("This budget is private right now", page.Markup);
        });
    }

    [Fact]
    public void SharingPage_AdminCanManageMembersAndInvitesButCannotTransferOwnership()
    {
        using var context = CreateSharingContext(BudgetMemberRole.Admin, includePeerMember: true);

        var page = context.Render<Sharing>();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Admin access - 3 active members", page.Markup);
            Assert.Contains("Create Invite", page.Markup);
            Assert.Contains("Members", page.Markup);
            Assert.Contains("Pending Invites", page.Markup);
            Assert.Contains("Update", page.Markup);
            Assert.Contains("Remove", page.Markup);
            Assert.Contains("Leave Budget", page.Markup);
            Assert.DoesNotContain("Make Owner", page.Markup);
            Assert.DoesNotContain("This budget is private right now", page.Markup);
        });
    }

    [Theory]
    [InlineData(BudgetMemberRole.Editor)]
    [InlineData(BudgetMemberRole.Viewer)]
    public void SharingPage_NonManagersCanLeaveButCannotSeeManagementControls(BudgetMemberRole role)
    {
        using var context = CreateSharingContext(role, includePeerMember: true);

        var page = context.Render<Sharing>();

        page.WaitForAssertion(() =>
        {
            Assert.Contains($"{role} access - 3 active members", page.Markup);
            Assert.Contains("Leave Budget", page.Markup);
            Assert.Contains("only Owners and Admins can manage members and invites", page.Markup);
            Assert.DoesNotContain("Create Invite", page.Markup);
            Assert.DoesNotContain("Pending Invites", page.Markup);
            Assert.DoesNotContain("Update", page.Markup);
            Assert.DoesNotContain("Remove", page.Markup);
            Assert.DoesNotContain("Make Owner", page.Markup);
            Assert.DoesNotContain("This budget is private right now", page.Markup);
        });
    }

    [Fact]
    public void SharingPage_PrivateSingleUserBudgetShowsSimpleEmptyState()
    {
        using var context = CreateSharingContext(BudgetMemberRole.Owner, includePeerMember: false);

        var page = context.Render<Sharing>();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Owner access - 1 active member", page.Markup);
            Assert.Contains("This budget is private right now", page.Markup);
            Assert.Contains("Create Invite", page.Markup);
            Assert.DoesNotContain("Leave Budget", page.Markup);
        });
    }

    private static BunitContext CreateSharingContext(BudgetMemberRole currentUserRole, bool includePeerMember)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var db = CreateDbContext();
        SeedSharingBudget(db, currentUserRole, includePeerMember);
        db.SaveChanges();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:DefaultUserId"] = "2",
                ["AppSettings:BaseUrl"] = "http://localhost/"
            })
            .Build();

        context.Services.AddSingleton<IConfiguration>(configuration);
        context.Services.AddSingleton(db);
        context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        context.Services.AddDataProtection();
        context.Services.AddSingleton<ProtectedSessionStorage>();
        context.Services.AddSingleton(provider =>
        {
            var authService = new AuthService(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ProtectedSessionStorage>(),
                provider.GetRequiredService<ClintonFranklandDbContext>(),
                provider.GetRequiredService<IHttpContextAccessor>());
            SetAuthenticatedUser(authService, db.Users.Single(user => user.UserId == 2));
            return authService;
        });
        context.Services.AddScoped<SiteInfoService>();
        context.Services.AddScoped<CurrentUserContext>();
        context.Services.AddScoped<SharedBudgetDataService>();
        context.Services.AddScoped<BudgetInviteService>(provider =>
            new BudgetInviteService(
                provider.GetRequiredService<ClintonFranklandDbContext>(),
                provider.GetRequiredService<SharedBudgetDataService>(),
                TimeProvider.System));

        return context;
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static void SeedSharingBudget(
        ClintonFranklandDbContext db,
        BudgetMemberRole currentUserRole,
        bool includePeerMember)
    {
        var now = DateTime.UtcNow;
        db.Users.AddRange(
            User(1, "owner", "Owner", "owner@example.com"),
            User(2, "current", "Current User", "current@example.com"),
            User(3, "peer", "Peer User", "peer@example.com"));
        db.SharedBudgets.Add(new SharedBudget
        {
            SharedBudgetId = 1,
            Name = "Household",
            OwnerUserId = currentUserRole == BudgetMemberRole.Owner ? 2 : 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        if (currentUserRole != BudgetMemberRole.Owner)
        {
            db.BudgetMembers.Add(new BudgetMember
            {
                BudgetMemberId = 1,
                SharedBudgetId = 1,
                UserId = 1,
                Role = BudgetMemberRole.Owner,
                Status = BudgetMemberStatus.Active,
                CreatedAtUtc = now
            });
        }

        db.BudgetMembers.Add(new BudgetMember
        {
            BudgetMemberId = 2,
            SharedBudgetId = 1,
            UserId = 2,
            Role = currentUserRole,
            Status = BudgetMemberStatus.Active,
            CreatedAtUtc = now
        });

        if (includePeerMember)
        {
            db.BudgetMembers.Add(new BudgetMember
            {
                BudgetMemberId = 3,
                SharedBudgetId = 1,
                UserId = 3,
                Role = BudgetMemberRole.Viewer,
                Status = BudgetMemberStatus.Active,
                CreatedAtUtc = now
            });
        }
    }

    private static void SetAuthenticatedUser(AuthService authService, User user)
    {
        typeof(AuthService)
            .GetField("_currentUser", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(authService, new UserInfo
            {
                UserId = user.UserId,
                SiteId = user.SiteId,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                EmailAddress = user.EmailAddress,
                IsAdmin = user.IsAdmin,
                ListButtonsRight = user.ListButtonsRight,
                IsLoggedIn = true
            });

        typeof(AuthService)
            .GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(authService, true);
    }

    private static User User(int id, string userName, string displayName, string? email = null) => new()
    {
        UserId = id,
        UserName = userName,
        DisplayName = displayName,
        EmailAddress = email,
        PasswordHash = "hash",
        Salt = "salt",
        IsAdmin = false,
        IsDeleted = false,
        FirstLogin = DateTime.UtcNow,
        LastLogin = DateTime.UtcNow
    };
}
