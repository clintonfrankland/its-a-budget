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

    public static TheoryData<string, string, string, string[]> OriginalIcons => new()
    {
        { "Checkbook", "add_task", "success", ["skip_next", "edit", "edit_calendar"] },
        { "Budget Forecast", "edit", "light", ["add_task", "skip_next", "edit_calendar"] },
        { "Budget Items", "edit", "light", ["add_task", "skip_next", "edit_calendar"] }
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
            Assert.Single(menuItem.QuerySelectorAll(".rzi"));
        });
        Assert.Single(primaryButton.QuerySelectorAll(".rzi"));
        Assert.Single(moreButton.QuerySelectorAll(".rzi"));
        Assert.DoesNotContain("Mark Paid", cut.Markup);
    }

    [Theory]
    [MemberData(nameof(OriginalIcons))]
    public void UsesTheExactOriginalRadzenButtonSizeShapeAndIcons(string page, string primaryIcon,
        string buttonStyle, string[] menuIcons)
    {
        var cut = Render(page, canManage: true);
        var primary = cut.Find(".budget-item-primary");

        Assert.Contains("rz-button-sm", primary.ClassList);
        Assert.Contains($"rz-{buttonStyle}", primary.ClassList);
        Assert.Equal(primaryIcon, primary.QuerySelector(".rzi")?.TextContent.Trim());
        Assert.Equal("more_vert", cut.Find(".budget-item-more .rzi").TextContent.Trim());
        Assert.Equal(menuIcons, cut.FindAll("[role=menuitem] .rzi").Select(icon => icon.TextContent.Trim()));
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

    [Fact]
    public void MoreButtonExplicitlyOpensAndMenuActionClosesTheOverflow()
    {
        var invoked = false;
        var cut = Render("Checkbook", true, onSkip: () => invoked = true);
        var more = cut.Find(".budget-item-more");

        Assert.Equal("false", more.GetAttribute("aria-expanded"));
        Assert.DoesNotContain("open", cut.Find(".budget-item-overflow").ClassList);

        more.Click();

        Assert.Equal("true", more.GetAttribute("aria-expanded"));
        Assert.Contains("open", cut.Find(".budget-item-overflow").ClassList);

        cut.Find("[aria-label='Skip']").Click();

        Assert.True(invoked);
        Assert.Equal("false", more.GetAttribute("aria-expanded"));
        Assert.DoesNotContain("open", cut.Find(".budget-item-overflow").ClassList);
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
        Assert.Equal(2, CountOccurrences(markup, "Width=\"70px\""));
    }

    [Fact]
    public void CompactActionPairDoesNotOverrideTheOriginalSmallButtonGeometry()
    {
        var css = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "wwwroot/css/app.css"));

        Assert.DoesNotContain("min-width:44px", css);
        Assert.DoesNotContain("min-height:44px", css);
        Assert.Contains(".budget-item-actions { display:inline-flex; align-items:center", css);
        Assert.Contains("inline-size:1.5rem !important; min-inline-size:1.5rem !important; max-inline-size:1.5rem !important; padding-inline:0 !important", css);
        Assert.Contains(".budget-item-overflow.open .budget-item-menu { display:grid; }", css);
        Assert.Contains(".rz-data-row:has(.budget-item-overflow.open) { position:relative; z-index:1001; }", css);
        Assert.Contains(".rz-data-row > td:has(.budget-item-overflow.open) { position:relative; z-index:1001; overflow:visible !important; }", css);
    }

    [Fact]
    public void MainLayoutLoadsBudgetSwitcherInAnIsolatedDependencyInjectionScope()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Components/Layout/MainLayout.razor.cs"));

        Assert.Contains("ScopeFactory.CreateAsyncScope()", source);
        Assert.Contains("scope.ServiceProvider.GetRequiredService<SharedBudgetDataService>()", source);
        Assert.DoesNotContain("private SharedBudgetDataService SharedBudgetsService", source);
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
