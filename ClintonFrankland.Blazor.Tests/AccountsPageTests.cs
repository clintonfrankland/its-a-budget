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
using Radzen;

namespace ClintonFrankland.Blazor.Tests;

public sealed class AccountsPageTests
{
    [Fact]
    public void AccountWithNoTransactionsRendersOpeningBalance()
    {
        using var context = CreateAccountsContext();
        var db = context.Services.GetRequiredService<ClintonFranklandDbContext>();

        var page = context.Render<Accounts>();

        page.WaitForAssertion(() =>
        {
            Assert.Empty(db.Transactions);
            Assert.Contains("Accounts", page.Markup);
            Assert.Contains("Household", page.Markup);
            Assert.Contains("Default", page.Markup);
            Assert.DoesNotContain("alert-danger", page.Markup);

            var account = Assert.Single(GetLoadedAccounts(page.Instance));
            Assert.Equal("Zero Transaction Checking", account.AccountName);
            Assert.Equal(125.45m, account.Balance);
        });
    }

    private static BunitContext CreateAccountsContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddRadzenComponents();

        var db = CreateDbContext();
        var user = new User
        {
            UserId = 42,
            UserName = "current",
            DisplayName = "Current User",
            EmailAddress = "current@example.com",
            IsDeleted = false
        };
        db.Users.Add(user);
        db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
        db.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 1, Name = "Household", OwnerUserId = 42 });
        db.BudgetMembers.Add(new BudgetMember
        {
            SharedBudgetId = 1,
            UserId = 42,
            Role = BudgetMemberRole.Owner,
            Status = BudgetMemberStatus.Active
        });
        db.Accounts.Add(new Account
        {
            AccountId = 1,
            AccountName = "Zero Transaction Checking",
            AccountTypeId = 1,
            BeginningBalance = 125.45m,
            Balance = 125.45m,
            ClearedBalance = 125.45m,
            UserId = 42,
            SharedBudgetId = 1
        });
        db.SaveChanges();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:DefaultUserId"] = "42",
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
            SetAuthenticatedUser(authService, user);
            return authService;
        });
        context.Services.AddScoped<SiteInfoService>();
        context.Services.AddScoped<CurrentUserContext>();
        context.Services.AddScoped<SharedBudgetDataService>();
        context.Services.AddScoped<AccountsDataService>();

        return context;
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static IReadOnlyList<AccountViewModel> GetLoadedAccounts(Accounts page) =>
        (IReadOnlyList<AccountViewModel>)typeof(Accounts)
            .GetField("accounts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(page)!;

    private static void SetAuthenticatedUser(AuthService authService, User user)
    {
        typeof(AuthService)
            .GetField("_currentUser", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(authService, new UserInfo
            {
                UserId = user.UserId,
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
