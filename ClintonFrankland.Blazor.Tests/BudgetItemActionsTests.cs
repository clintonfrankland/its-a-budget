using Bunit;
using ClintonFrankland.Components;
using ClintonFrankland.Components.Pages;
using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Microsoft.AspNetCore.Components;
using System.Reflection;

namespace ClintonFrankland.Blazor.Tests;

public class BudgetItemActionsTests : BunitContext
{
    public static TheoryData<string, string, string[]> PageLayouts => new()
    {
        { "Checkbook", "Record to Checkbook", ["Skip", "Edit", "Edit Next"] },
        { "Budget Forecast", "Edit", ["Record to Checkbook", "Skip", "Edit Next"] },
        { "Budget Items", "Edit", ["Record to Checkbook", "Skip", "Edit Next"] }
    };

    [Theory]
    [MemberData(nameof(PageLayouts))]
    public void ManageableRowRendersIconOnlyPrimaryAndOrderedAccessibleOverflow(string page, string primary, string[] menu)
    {
        var cut = Render(page, canManage: true);
        var primaryButton = cut.Find(".budget-item-primary");
        var moreButton = cut.Find(".budget-item-more");
        Assert.Equal(primary, primaryButton.GetAttribute("aria-label"));
        Assert.Equal(primary, primaryButton.GetAttribute("title"));
        Assert.DoesNotContain(primary, primaryButton.TextContent.Trim());
        Assert.Equal("More budget item actions", moreButton.GetAttribute("aria-label"));
        Assert.Equal("More budget item actions", moreButton.GetAttribute("title"));
        Assert.DoesNotContain("More budget item actions", moreButton.TextContent.Trim());
        Assert.Equal(menu, cut.FindAll("[role=menuitem]").Select(MenuItemLabel));
        Assert.All(cut.FindAll("[role=menuitem]"), menuItem =>
        {
            Assert.Equal(MenuItemLabel(menuItem), menuItem.GetAttribute("aria-label"));
            Assert.Single(menuItem.QuerySelectorAll("svg.budget-item-action-icon"));
            Assert.Empty(menuItem.QuerySelectorAll(".rz-icon"));
        });
        Assert.Single(primaryButton.QuerySelectorAll("svg.budget-item-action-icon"));
        Assert.Single(moreButton.QuerySelectorAll("svg.budget-item-action-icon"));
        Assert.Empty(cut.FindAll(".rz-icon"));
        Assert.DoesNotContain("add_task", cut.Markup);
        Assert.DoesNotContain("more_vert", cut.Markup);
        Assert.DoesNotContain("skip_next", cut.Markup);
        Assert.DoesNotContain("event_repeat", cut.Markup);
        Assert.DoesNotContain("Mark Paid", cut.Markup);
    }

    [Theory]
    [InlineData("Checkbook")]
    [InlineData("Budget Forecast")]
    [InlineData("Budget Items")]
    public void ReadOnlyRowHidesAllActions(string page) => Assert.Empty(Render(page, false).FindAll("button"));

    [Fact]
    public void DisabledAndProgressStatesPreventActivation()
    {
        var invoked = 0;
        var cut = Render("Checkbook", true, false, true, () => invoked++);
        Assert.All(cut.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
        Assert.Equal("true", cut.Find(".budget-item-actions").GetAttribute("aria-busy"));
        cut.Find(".budget-item-primary").Click();
        Assert.Equal(0, invoked);
    }

    [Theory]
    [MemberData(nameof(PageLayouts))]
    public void EveryActionPathInvokesItsExistingWorkflowCallback(string page, string primary, string[] menu)
    {
        var calls = new List<string>();
        var cut = Render(page, true, onRecord: () => calls.Add("record"), onSkip: () => calls.Add("skip"),
            onEdit: () => calls.Add("edit"), onEditNext: () => calls.Add("edit-next"));
        Assert.Equal(primary, cut.Find(".budget-item-primary").GetAttribute("aria-label"));
        cut.Find(".budget-item-primary").Click();
        foreach (var label in menu)
            cut.FindAll("[role=menuitem]").Single(button => button.GetAttribute("aria-label") == label).Click();
        Assert.Equal(page == "Checkbook" ? ["record", "skip", "edit", "edit-next"] : ["edit", "record", "skip", "edit-next"], calls);
    }

    [Theory]
    [InlineData("Components/Pages/Checkbook.razor")]
    [InlineData("Components/Pages/Budget.razor")]
    [InlineData("Components/Pages/BudgetItems.razor")]
    public void EveryBudgetItemListUsesTheSharedCompactActionPair(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));

        Assert.Equal(2, CountOccurrences(markup, "<BudgetItemActions"));
        Assert.Equal(2, CountOccurrences(markup, "Width=\"96px\""));
    }

    [Fact]
    public void CompactActionPairKeepsExplicitPhoneTouchTargets()
    {
        var css = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "wwwroot/css/app.css"));

        Assert.Contains(".budget-item-primary { display:inline-grid; place-items:center; min-width:44px; min-height:44px", css);
        Assert.Contains(".budget-item-more { display:grid; place-items:center; border-radius:0 .25rem .25rem 0; min-width:44px; min-height:44px", css);
    }

    [Theory]
    [InlineData("/budgetitems?edit=101", "Edit Budget")]
    [InlineData("/budget?editNext=101", "Edit Next Occurrence")]
    public void QueryDrivenEditDestinationsOpenAuthorizedPageLocalEditors(string uri, string heading)
    {
        ConfigurePageServices(BudgetMemberRole.Editor);
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

        var cut = RenderRouter();

        cut.WaitForAssertion(() => Assert.Contains(heading, cut.Markup));
        Assert.Contains("Managed budget", cut.Markup);
        Assert.Contains("Save", cut.Markup);
    }

    [Theory]
    [InlineData("/budgetitems?edit=101", "Edit Budget")]
    [InlineData("/budget?editNext=101", "Edit Next Occurrence")]
    public void QueryDrivenEditDestinationsDenyReadOnlyBudgetItems(string uri, string forbiddenHeading)
    {
        ConfigurePageServices(BudgetMemberRole.Viewer);
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

        var cut = RenderRouter();

        cut.WaitForAssertion(() => Assert.Contains("Read-only budget", cut.Markup));
        Assert.DoesNotContain(forbiddenHeading, cut.Markup);
    }

    private IRenderedComponent<Router> RenderRouter() => Render<Router>(parameters => parameters
        .Add(x => x.AppAssembly, typeof(ClintonFrankland.Components.Pages.Budget).Assembly)
        .Add(x => x.Found, routeData => builder =>
        {
            builder.OpenComponent<RouteView>(0);
            builder.AddAttribute(1, nameof(RouteView.RouteData), routeData);
            builder.CloseComponent();
        }));

    private void ConfigurePageServices(BudgetMemberRole role)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.AddStub<RadzenChart>();
        Services.AddRadzenComponents();
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ClintonFranklandDbContext(options);
        db.Users.Add(new User { UserId = 7, UserName = "tester", DisplayName = "Tester" });
        db.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 50, Name = "Test household", OwnerUserId = 8 });
        db.BudgetMembers.Add(new BudgetMember { SharedBudgetId = 50, UserId = 7, Role = role });
        db.Categories.Add(new Category { CategoryId = 10, CategoryName = "Housing", UserId = 8, SharedBudgetId = 50 });
        db.Frequencies.Add(new Frequency { FrequencyId = 1, FrequencyName = "Monthly", Sort = 1 });
        db.Budgets.AddRange(
            new ClintonFrankland.Models.Entities.Budget { BudgetId = 101, BudgetName = role == BudgetMemberRole.Viewer ? "Read-only budget" : "Managed budget", BudgetTypeId = 1, CategoryId = 10, FrequencyId = 1, UserId = 8, SharedBudgetId = 50, NextDueDate = DateTime.Today, Amount = 25m },
            new ClintonFrankland.Models.Entities.Budget { BudgetId = 102, BudgetName = "Read-only budget", BudgetTypeId = 1, CategoryId = 10, FrequencyId = 1, UserId = 8, SharedBudgetId = 50, NextDueDate = DateTime.Today.AddDays(1), Amount = 30m });
        db.SaveChanges();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppSettings:DefaultUserId"] = "7"
        }).Build();
        var auth = new AuthService(configuration,
            new ProtectedSessionStorage(JSInterop.JSRuntime, new EphemeralDataProtectionProvider()),
            db, new Microsoft.AspNetCore.Http.HttpContextAccessor());
        typeof(AuthService).GetField("_currentUser", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(auth, new UserInfo { UserId = 7, UserName = "tester", DisplayName = "Tester", IsLoggedIn = true });
        typeof(AuthService).GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(auth, true);
        var shared = new SharedBudgetDataService(db);
        Services.AddSingleton(db);
        Services.AddSingleton(auth);
        Services.AddSingleton(new SiteInfoService(configuration));
        Services.AddSingleton<CurrentUserContext>();
        Services.AddSingleton(shared);
        Services.AddSingleton(new BudgetDataService(db, shared));
        Services.AddSingleton(new BudgetItemsDataService(db, shared));
        Services.AddSingleton(new BudgetScheduleService(db, shared));
        Services.AddSingleton(new CheckbookDataService(db, shared));
        Services.AddSingleton<BudgetItemsExportService>();
        Services.AddSingleton<DialogService>();
    }

    private IRenderedComponent<BudgetItemActions> Render(string page, bool canManage, bool canRecord = true,
        bool inProgress = false, Action? onRecord = null, Action? onSkip = null, Action? onEdit = null, Action? onEditNext = null) =>
        Render<BudgetItemActions>(parameters => parameters
            .Add(x => x.Page, page).Add(x => x.CanManage, canManage).Add(x => x.CanRecordToCheckbook, canRecord)
            .Add(x => x.IsInProgress, inProgress)
            .Add(x => x.OnRecord, EventCallback.Factory.Create(this, onRecord ?? (() => { })))
            .Add(x => x.OnSkip, EventCallback.Factory.Create(this, onSkip ?? (() => { })))
            .Add(x => x.OnEdit, EventCallback.Factory.Create(this, onEdit ?? (() => { })))
            .Add(x => x.OnEditNext, EventCallback.Factory.Create(this, onEditNext ?? (() => { }))));

    private static int CountOccurrences(string value, string search) => value.Split(search, StringSplitOptions.None).Length - 1;

    private static string MenuItemLabel(AngleSharp.Dom.IElement menuItem) => menuItem.Children.Last().TextContent.Trim();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ClintonFrankland.Blazor.csproj")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
