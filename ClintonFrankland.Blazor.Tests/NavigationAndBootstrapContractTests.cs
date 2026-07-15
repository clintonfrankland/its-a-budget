using ClintonFrankland.Data;
using ClintonFrankland.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClintonFrankland.Blazor.Tests;

public class NavigationAndBootstrapContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void MainNavigation_IsGroupedAndDoesNotExposeDuplicateInsightsLink()
    {
        var markup = Read("Components/Layout/MainLayout.razor");

        Assert.Contains(">Home</a>", markup);
        Assert.Contains(">Checkbook</a>", markup);
        Assert.Contains(">Plan</button>", markup);
        Assert.Contains(">Reports</a>", markup);
        Assert.Contains(">Manage</button>", markup);
        Assert.Contains("data-bs-target=\"#navbar\"", markup);
        Assert.Contains("Profile &amp; Notifications", markup);
        Assert.DoesNotContain("href=\"insights\"", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Insights</a>", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MobileNavigation_ClosesBootstrapCollapseAfterEnhancedNavigation()
    {
        var layout = Read("Components/Layout/MainLayout.razor.cs");
        var javascript = Read("wwwroot/js/download.js");

        Assert.Contains("budgetApp.collapseNavbar", layout);
        Assert.Contains("OnLocationChanged", layout);
        Assert.Contains("Collapse.getOrCreateInstance", javascript);
        Assert.Contains(".hide()", javascript);
    }

    [Fact]
    public void LegacyInsightsRoute_RedirectsToSpendingReports()
    {
        var source = Read("Components/Pages/Insights.razor.cs");
        Assert.Contains("/reports?view=spending", source);
        Assert.Contains("replace: true", source);
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
}
