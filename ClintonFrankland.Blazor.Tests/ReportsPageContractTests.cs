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
        Assert.Equal(4, code.Split("catch {", StringSplitOptions.None).Length - 1);
    }
}
