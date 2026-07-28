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

/// <summary>Exercises the rendered review inbox against the real reconciliation service.</summary>
public sealed class PlaidReconciliationUiLifecycleTests
{
    [Fact]
    public void ConfirmDeferAndIgnore_AreRenderedAndDriveTheReviewLifecycle()
    {
        using var context = CreateContext();
        var database = context.Services.GetRequiredService<ClintonFranklandDbContext>();
        AddStaged(database, 1, "Confirm", 24m); AddLedger(database, 1, -24m, "Confirm");
        AddStaged(database, 2, "Defer", 25m); AddLedger(database, 2, -25m, "Defer");
        AddStaged(database, 3, "Ignore", 26m); AddLedger(database, 3, -26m, "Ignore");
        database.SaveChanges();

        var page = context.Render<PlaidReconciliation>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("article").Count));

        ActionButton(page, 0, "Confirm and clear").Click();
        page.WaitForAssertion(() => Assert.Contains("Match confirmed", page.Markup));
        Assert.True(database.Transactions.Single(transaction => transaction.TransactionId == 1).Cleared);
        Assert.Equal(PlaidReconciliationReviewState.Confirmed, database.PlaidTransactionStaging.Single(item => item.PlaidTransactionStagingId == 1).ReviewState);

        ActionButton(page, 1, "Defer").Click();
        page.WaitForAssertion(() => Assert.Contains("Review state saved", page.Markup));
        Assert.Equal(PlaidReconciliationReviewState.Deferred, database.PlaidTransactionStaging.Single(item => item.PlaidTransactionStagingId == 2).ReviewState);

        ActionButton(page, 2, "Ignore").Click();
        page.WaitForAssertion(() => Assert.Contains("Review state saved", page.Markup));
        Assert.Equal(PlaidReconciliationReviewState.Ignored, database.PlaidTransactionStaging.Single(item => item.PlaidTransactionStagingId == 3).ReviewState);
    }

    [Fact]
    public void PendingUnauthorizedAndModifiedRecords_RenderSafeReviewStates()
    {
        using var context = CreateContext();
        var database = context.Services.GetRequiredService<ClintonFranklandDbContext>();
        AddStaged(database, 1, "Pending", 24m, isPending: true); AddLedger(database, 1, -24m, "Pending");
        AddStaged(database, 2, "Unauthorized", 25m); AddLedger(database, 2, -25m, "Unauthorized", userId: 2);
        AddStaged(database, 3, "Preserved", 26m, isRemoved: true, confirmed: true); AddLedger(database, 3, -26m, "Preserved", cleared: true);
        database.SaveChanges();

        var page = context.Render<PlaidReconciliation>();
        page.WaitForAssertion(() =>
        {
            Assert.Contains("Pending bank records", page.Markup);
            Assert.Contains("No eligible ledger transaction matched", page.Markup);
            Assert.Contains("remains cleared and has not been deleted or uncleared", page.Markup);
        });
        Assert.All(page.FindAll("article").Where(article => article.TextContent.Contains("Pending") || article.TextContent.Contains("Unauthorized")),
            article => Assert.True(article.QuerySelector("button.rz-success")?.HasAttribute("disabled") == true));
        Assert.True(database.Transactions.Single(transaction => transaction.TransactionId == 3).Cleared);
    }

    [Fact]
    public void OnlyCurrentUnmatchedPostedRecords_OfferAddToCheckbook()
    {
        using var context = CreateContext();
        var database = context.Services.GetRequiredService<ClintonFranklandDbContext>();
        AddStaged(database, 1, "Posted unmatched", 24m);
        AddStaged(database, 2, "Pending unmatched", 25m, isPending: true);
        AddStaged(database, 3, "Removed unmatched", 26m, isRemoved: true);
        database.SaveChanges();

        var page = context.Render<PlaidReconciliation>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("article").Count));

        Assert.Contains("Add to Checkbook", Article(page, "Posted unmatched").TextContent);
        Assert.DoesNotContain("Add to Checkbook", Article(page, "Pending unmatched").TextContent);
        Assert.DoesNotContain("Add to Checkbook", Article(page, "Removed unmatched").TextContent);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddRadzenComponents();
        var database = new ClintonFranklandDbContext(new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { UserId = 1, UserName = "current", DisplayName = "Current User" };
        database.Users.Add(user); database.Accounts.Add(new Account { AccountId = 10, AccountName = "Checking", UserId = 1 }); database.SaveChanges();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AppSettings:DefaultUserId"] = "1", ["AppSettings:BaseUrl"] = "http://localhost/" }).Build();
        context.Services.AddSingleton<IConfiguration>(configuration); context.Services.AddSingleton(database); context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        context.Services.AddDataProtection(); context.Services.AddSingleton<ProtectedSessionStorage>();
        context.Services.AddSingleton(provider => AuthenticatedAuthService(provider, user));
        context.Services.AddScoped<SiteInfoService>(); context.Services.AddScoped<CurrentUserContext>(); context.Services.AddScoped<PlaidReconciliationService>();
        return context;
    }

    private static AuthService AuthenticatedAuthService(IServiceProvider provider, User user)
    {
        var auth = new AuthService(provider.GetRequiredService<IConfiguration>(), provider.GetRequiredService<ProtectedSessionStorage>(), provider.GetRequiredService<ClintonFranklandDbContext>(), provider.GetRequiredService<IHttpContextAccessor>());
        typeof(AuthService).GetField("_currentUser", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(auth, new UserInfo { UserId = user.UserId, UserName = user.UserName, DisplayName = user.DisplayName, IsLoggedIn = true });
        typeof(AuthService).GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(auth, true);
        return auth;
    }

    private static AngleSharp.Dom.IElement ActionButton(IRenderedComponent<PlaidReconciliation> page, int recordIndex, string action) =>
        page.FindAll("article")[recordIndex]
            .QuerySelectorAll("button").First(button => button.TextContent.Contains(action));

    private static AngleSharp.Dom.IElement Article(IRenderedComponent<PlaidReconciliation> page, string description) =>
        page.FindAll("article").Single(article => article.TextContent.Contains(description));

    private static void AddStaged(ClintonFranklandDbContext database, int id, string name, decimal amount, bool isPending = false, bool isRemoved = false, bool confirmed = false) =>
        database.PlaidTransactionStaging.Add(new PlaidTransactionStaging { PlaidTransactionStagingId = id, UserId = 1, PlaidItemId = 1, BudgetAccountId = 10, PlaidTransactionId = $"staged-{id}", PlaidAccountId = "account", PlaidAmount = amount, TransactionDate = new DateOnly(2026, 7, 27), Name = name, IsPending = isPending, IsRemoved = isRemoved, ReviewState = confirmed ? PlaidReconciliationReviewState.Confirmed : PlaidReconciliationReviewState.Pending, LinkedTransactionId = confirmed ? id : null, LinkedSourceFingerprint = confirmed ? "old-fingerprint" : null });

    private static void AddLedger(ClintonFranklandDbContext database, int id, decimal amount, string payee, int userId = 1, bool cleared = false) =>
        database.Transactions.Add(new Transaction { TransactionId = id, AccountId = 10, UserId = userId, Amount = amount, PayeeId = id, CategoryId = 1, Payee = new Payee { PayeeId = id, UserId = userId, PayeeName = payee }, TransactionDate = new DateOnly(2026, 7, 27), Cleared = cleared });
}
