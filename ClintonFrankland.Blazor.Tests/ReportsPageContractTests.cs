namespace ClintonFrankland.Blazor.Tests;

public class ReportsPageContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void Reports_AuthenticatesBeforeQueriesAndRedirectsWithReportsReturnUrl()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Components/Pages/Reports.razor.cs"));
        var initialize = source.IndexOf("await AuthService.InitializeAsync()", StringComparison.Ordinal);
        var authGuard = source.IndexOf("if (!AuthService.IsAuthenticated)", initialize, StringComparison.Ordinal);
        var redirect = source.IndexOf("Uri.EscapeDataString(\"/reports\")", authGuard, StringComparison.Ordinal);
        var earlyReturn = source.IndexOf("return;", redirect, StringComparison.Ordinal);
        var firstQuery = source.IndexOf("await LoadAllAsync()", StringComparison.Ordinal);

        Assert.True(initialize >= 0 && authGuard > initialize && redirect > authGuard && earlyReturn > redirect);
        Assert.True(firstQuery > earlyReturn);
    }

    [Fact]
    public void Reports_ProvidesIndependentEmptyAndFailureStateForEveryReport()
    {
        var markup = File.ReadAllText(Path.Combine(RepoRoot, "Components/Pages/Reports.razor"));
        var code = File.ReadAllText(Path.Combine(RepoRoot, "Components/Pages/Reports.razor.cs"));

        Assert.Contains("spendError", markup);
        Assert.Contains("spendPlan.Count == 0", markup);
        Assert.Contains("trendError", markup);
        Assert.Contains("trends.Count == 0", markup);
        Assert.Contains("cashflowError", markup);
        Assert.Contains("cashflow is not null", markup);
        Assert.Contains("netWorthError", markup);
        Assert.Contains("netWorth is not null", markup);
        Assert.Contains("payeeError", markup);
        Assert.Contains("topPayees.Count == 0", markup);
        Assert.Equal(5, code.Split("catch {", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Reports_UsesFourReadableSectionsAndLinksToEditingWorkflows()
    {
        var markup = File.ReadAllText(Path.Combine(RepoRoot, "Components/Pages/Reports.razor"));

        Assert.Contains(">Overview</button>", markup);
        Assert.Contains(">Spending</button>", markup);
        Assert.Contains(">Cash Flow</button>", markup);
        Assert.Contains(">Net Worth</button>", markup);
        Assert.Contains("href=\"/budgetitems\"", markup);
        Assert.Contains("href=\"/budget\"", markup);
        Assert.Contains("Take(5)", markup);
    }

    [Fact]
    public void Spending_IncludesAccessibleResponsiveMonthCloseSummaryStatusesAndEmptyState()
    {
        var markup = File.ReadAllText(Path.Combine(RepoRoot, "Components/Pages/Reports.razor"));
        var styles = File.ReadAllText(Path.Combine(RepoRoot, "wwwroot/css/app.css"));
        var viewModels = File.ReadAllText(Path.Combine(RepoRoot, "Models/ViewModels/ReportsViewModels.cs"));

        Assert.Contains("Month-close variance snapshot", markup);
        Assert.Contains("Total budgeted", markup);
        Assert.Contains("Total actual", markup);
        Assert.Contains("Total variance", markup);
        Assert.Contains("Needs attention", markup);
        Assert.Contains("Largest unspent amounts", markup);
        Assert.Contains("@row.Status", markup);
        Assert.Contains("Nothing to close for @selectedMonth", markup);
        Assert.Contains("role=\"status\"", markup);
        Assert.Contains("aria-labelledby=\"varianceSummaryHeading\"", markup);
        Assert.Contains("table-responsive variance-detail", markup);
        Assert.Contains(".variance-totals", styles);
        Assert.Contains("grid-template-columns: 1fr", styles);
        Assert.Contains("Over budget", viewModels);
        Assert.Contains("Unbudgeted", viewModels);
        Assert.Contains("Under budget", viewModels);
        Assert.Contains("On budget", viewModels);
    }
}
