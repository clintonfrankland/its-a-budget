namespace ClintonFrankland.Blazor.Tests;

/// <summary>Locks the page's review lifecycle controls to the service's safe action contract.</summary>
public sealed class PlaidReconciliationUiLifecycleTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void Inbox_RendersExplicitSafeActionsAndPreservationAlert()
    {
        var markup = File.ReadAllText(Path.Combine(RepositoryRoot, "Components/Pages/PlaidReconciliation.razor"));
        var code = File.ReadAllText(Path.Combine(RepositoryRoot, "Components/Pages/PlaidReconciliation.razor.cs"));

        Assert.Contains("Confirm and clear", markup, StringComparison.Ordinal);
        Assert.Contains("Defer", markup, StringComparison.Ordinal);
        Assert.Contains("Ignore", markup, StringComparison.Ordinal);
        Assert.Contains("remains cleared and has not been deleted or uncleared", markup, StringComparison.Ordinal);
        Assert.Contains("ConfirmAsync(CurrentUser.UserId", code, StringComparison.Ordinal);
        Assert.Contains("SetReviewStateAsync(CurrentUser.UserId", code, StringComparison.Ordinal);
        Assert.Contains("PlaidReconciliationActionResult.Stale", code, StringComparison.Ordinal);
        Assert.Contains("PlaidReconciliationActionResult.AlreadyReviewed", code, StringComparison.Ordinal);
        Assert.Contains("PlaidReconciliationActionResult.Unauthorized", code, StringComparison.Ordinal);
    }
}
