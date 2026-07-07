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

public sealed class SettingsPageTests
{
    [Fact]
    public void PasswordUtilityCancelButtonClosesModal()
    {
        using var context = CreateSettingsContext();
        var db = context.Services.GetRequiredService<ClintonFranklandDbContext>();
        var page = context.Render<Settings>();

        page.WaitForAssertion(() => Assert.Contains("Users", page.Markup));
        OpenPasswordUtility(page.Instance, db.Users.Single(user => user.UserName == "current"));

        page.Render();
        page.WaitForAssertion(() => Assert.Contains("Password Utility - current", page.Markup));

        var cancelButton = page.FindAll("button")
            .Single(button => button.TextContent.Trim() == "Cancel");

        Assert.Equal("button", cancelButton.GetAttribute("type"));

        cancelButton.Click();

        page.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Password Utility - current", page.Markup);
        });
    }

    [Fact]
    public async Task ResetPasswordClosesModalAndShowsSuccessMessage()
    {
        using var context = CreateSettingsContext();
        var db = context.Services.GetRequiredService<ClintonFranklandDbContext>();
        var page = context.Render<Settings>();

        page.WaitForAssertion(() => Assert.Contains("Users", page.Markup));
        OpenPasswordUtility(page.Instance, db.Users.Single(user => user.UserName == "current"));
        SetPrivateProperty(page.Instance, "PasswordValue", "NewLocalPassword!23");

        await InvokeResetPasswordAsync(page.Instance);
        page.Render();

        var user = db.Users.Single(user => user.UserName == "current");
        Assert.True(PasswordUtility.VerifyPassword("NewLocalPassword!23", user.Salt, user.PasswordHash));
        page.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Password Utility - current", page.Markup);
            Assert.Contains("Password reset for current.", page.Markup);
        });
    }

    private static BunitContext CreateSettingsContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var db = CreateDbContext();
        var salt = PasswordUtility.CreateSalt();
        var currentUser = new User
        {
            UserId = 2,
            SiteId = 1,
            UserName = "current",
            DisplayName = "Current User",
            EmailAddress = "current@example.com",
            PasswordHash = PasswordUtility.HashPassword("old-password", salt),
            Salt = salt,
            IsAdmin = false,
            IsDeleted = false,
            FirstLogin = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        db.Users.Add(currentUser);
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
            SetAuthenticatedUser(authService, currentUser);
            return authService;
        });
        context.Services.AddScoped<EmailSenderService>();
        context.Services.AddScoped<ExternalIdentityLinkService>();
        context.Services.AddScoped<MigrationErrorTracker>();
        context.Services.AddSingleton(TimeProvider.System);

        return context;
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static void OpenPasswordUtility(Settings page, User user) =>
        typeof(Settings)
            .GetMethod("OpenPasswordUtility", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, [user]);

    private static async Task InvokeResetPasswordAsync(Settings page)
    {
        var task = (Task)typeof(Settings)
            .GetMethod("ResetPassword", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, [])!;

        await task;
    }

    private static void SetPrivateProperty(Settings page, string propertyName, string value) =>
        typeof(Settings)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, value);

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
}
