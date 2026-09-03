using ClintonFrankland.Data;
using ClintonFrankland.Components.Layout;
using ClintonFrankland.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.JSInterop;

namespace ClintonFrankland.Blazor.Tests;

public class NavigationAndBootstrapContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void MainNavigation_IsGroupedAndDoesNotExposeDuplicateInsightsLink()
    {
        var markup = Read("Components/Layout/MainLayout.razor");
        var app = Read("Components/App.razor");

        Assert.Contains("aria-label=\"It's a Budget home\"", markup);
        Assert.DoesNotContain("<a class=\"nav-link\" href=\"/\"", markup);
        Assert.Contains(">Checkbook</a>", markup);
        Assert.Contains(">Plan</button>", markup);
        Assert.Contains(">Reports</a>", markup);
        Assert.Contains(">Manage</button>", markup);
        Assert.Contains("data-bs-target=\"#navbar\"", markup);
        Assert.Contains("Profile &amp; Notifications", markup);
        Assert.Contains("<strong class=\"d-block\">Forecast</strong>", markup);
        Assert.Contains("<small class=\"d-block text-secondary mt-1\">Projected balances and upcoming items</small>", markup);
        Assert.Contains("Scheduled transactions and spending allowances", markup);
        Assert.DoesNotContain("href=\"/category-budgets\"", markup);
        Assert.Contains("css/app.css?v=", app);
        Assert.DoesNotContain("href=\"insights\"", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Insights</a>", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BudgetItemsGrid_PreservesNameAtHalfScreenAndDefersDetailColumns()
    {
        var markup = Read("Components/Pages/BudgetItems.razor");

        Assert.Contains("Property=\"BudgetName\" Title=\"Budget Name\"", markup);
        Assert.Contains("Property=\"Category\" Title=\"Category\" Visible=\"@IsAtLeast(ScreenSize.Large)\"", markup);
        Assert.Contains("Title=\"Current\" Width=\"135px\" TextAlign=\"TextAlign.Right\" Visible=\"@IsAtLeast(ScreenSize.Medium)\"", markup);
        Assert.Contains("Property=\"DueDate\" Title=\"Due / Reset\" Width=\"105px\" FormatString=\"{0:MM/dd/yyyy}\" Visible=\"@IsAtLeast(ScreenSize.ExtraExtraLarge)\"", markup);
        Assert.Contains("Property=\"EndDateName\" Title=\"End Date\" Width=\"100px\" Visible=\"@IsAtLeast(ScreenSize.ExtraExtraLarge)\"", markup);
        Assert.Contains("Property=\"FrequencyName\" Title=\"Frequency\" Width=\"100px\" Visible=\"@IsAtLeast(ScreenSize.ExtraExtraLarge)\"", markup);
        Assert.Contains("Property=\"Monthly\" Title=\"Monthly\" Width=\"100px\" FormatString=\"{0:C}\" TextAlign=\"TextAlign.Right\" Visible=\"@IsAtLeast(ScreenSize.ExtraExtraLarge)\"", markup);
        Assert.Contains("Title=\"3-mo Trend\" Width=\"100px\" Sortable=\"false\" Filterable=\"false\" TextAlign=\"TextAlign.Center\" Visible=\"@IsAtLeast(ScreenSize.ExtraExtraLarge)\"", markup);
        Assert.DoesNotContain("Property=\"BudgetName\" Title=\"Budget Name\" Visible=", markup);
    }

    [Fact]
    public async Task MobileNavigation_IsVersionedAndCannotBreakTheCircuitWhenTheScriptIsStale()
    {
        var layout = Read("Components/Layout/MainLayout.razor.cs");
        var javascript = Read("wwwroot/js/download.js");
        var app = Read("Components/App.razor");

        Assert.Contains("budgetApp.collapseNavbar", layout);
        Assert.Contains("OnLocationChanged", layout);
        Assert.Contains("catch (JSException)", layout);
        Assert.Contains("Collapse.getOrCreateInstance", javascript);
        Assert.Contains(".hide()", javascript);
        Assert.Contains("js/download.js?v=", app);

        await MainLayout.TryCollapseNavbarAsync(new MissingNavbarFunctionJsRuntime(), "navbar");
    }

    [Fact]
    public void LegacyInsightsRoute_RedirectsToSpendingReports()
    {
        var source = Read("Components/Pages/Insights.razor.cs");
        Assert.Contains("/reports?view=spending", source);
        Assert.Contains("replace: true", source);
    }

    [Fact]
    public void LegacyCategoryBudgetsRoute_RedirectsToUnifiedBudgetItems()
    {
        var source = Read("Components/Pages/CategoryBudgets.razor.cs");
        Assert.Contains("/budgetitems?kind=allowances", source);
        Assert.Contains("replace: true", source);
    }

    [Fact]
    public void SpendingAllowanceMigration_IsGuardedAndMigratesLatestCategoryTargets()
    {
        var source = Read("Migrations/20260715192503_AddSpendingAllowanceBudgetItems.cs");
        Assert.Contains("COL_LENGTH('dbo.cfBudgets', 'IsSpendingAllowance') IS NULL", source);
        Assert.Contains("ROW_NUMBER() OVER", source);
        Assert.Contains("TargetRank = 1", source);
        Assert.Contains("IsSpendingAllowance = 1", source);
    }

    [Theory]
    [InlineData("Components/Pages/BudgetItems.razor")]
    [InlineData("Components/Pages/Budget.razor")]
    public void AllowanceEditor_KeepsNameVisibleAndHidesOnlyTransactionType(string path)
    {
        var source = Read(path);
        var behavior = source.IndexOf("Text=\"Behavior:\"", StringComparison.Ordinal);
        var name = source.IndexOf("Name=\"editBudgetNameTextBox\"", behavior, StringComparison.Ordinal);
        var conditional = source.IndexOf("@if (!editIsSpendingAllowance)", name, StringComparison.Ordinal);
        var type = source.IndexOf("Text=\"Type:\"", conditional, StringComparison.Ordinal);

        Assert.True(behavior >= 0 && name > behavior && conditional > name && type > conditional);
    }

    [Fact]
    public void NewAdminCreatedUser_GetsPrivateBudgetMembershipImmediately()
    {
        var source = Read("Components/Pages/Settings.razor.cs");
        var save = source.IndexOf("await DbContext.SaveChangesAsync()", StringComparison.Ordinal);
        var bootstrap = source.IndexOf("GetDefaultSharedBudgetIdAsync", save, StringComparison.Ordinal);

        Assert.True(save >= 0 && bootstrap > save);
    }

    [Fact]
    public void TransactionRuleRepairMigration_IsDiscoverableAndGuarded()
    {
        var type = typeof(RepairTransactionRulesTable);
        var migration = Assert.Single(type.GetCustomAttributes(typeof(MigrationAttribute), false).Cast<MigrationAttribute>());
        Assert.Equal("20260714172041_RepairTransactionRulesTable", migration.Id);
        Assert.Single(type.GetCustomAttributes(typeof(DbContextAttribute), false).Cast<DbContextAttribute>());

        var source = Read("Migrations/20260714172041_RepairTransactionRulesTable.cs");
        Assert.Contains("IF OBJECT_ID('dbo.cfTransactionRules', 'U') IS NULL", source);
        Assert.Contains("NOT EXISTS (SELECT 1 FROM sys.indexes", source);
        Assert.Contains("migrationBuilder.Sql", source);
    }

    [Fact]
    public void ExistingTransactionlessAccountBalanceRepair_IsDiscoverableAndConservative()
    {
        var type = typeof(ReconcileTransactionlessAccountBalances);
        var migration = Assert.Single(type.GetCustomAttributes(typeof(MigrationAttribute), false).Cast<MigrationAttribute>());
        Assert.Equal("20260715114000_ReconcileTransactionlessAccountBalances", migration.Id);
        Assert.Single(type.GetCustomAttributes(typeof(DbContextAttribute), false).Cast<DbContextAttribute>());

        var source = Read("Migrations/20260715114000_ReconcileTransactionlessAccountBalances.cs");
        Assert.Contains("accountRow.BeginningBalance = 0", source);
        Assert.Contains("accountRow.Balance <> 0", source);
        Assert.Contains("NOT EXISTS (", source);
        Assert.Contains("transactionRow.AccountId = accountRow.AccountId", source);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(RepoRoot, path));

    private sealed class MissingNavbarFunctionJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new JSException("The value 'budgetApp.collapseNavbar' is not a function.");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            throw new JSException("The value 'budgetApp.collapseNavbar' is not a function.");
    }
}
