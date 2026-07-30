namespace ClintonFrankland.Blazor.Tests;

public class QuickAddTransactionPageTests
{
    [Fact]
    public void Checkbook_ExposesHiddenQuickAddRouteAndChallengesAuthentik()
    {
        var markup = ReadRepoFile("Components/Pages/Checkbook.razor");
        var code = ReadRepoFile("Components/Pages/Checkbook.razor.cs");
        var navigation = ReadRepoFile("Components/Layout/MainLayout.razor");

        Assert.Contains("@page \"/quick-add\"", markup, StringComparison.Ordinal);
        Assert.Contains("/auth/authentik/login?returnUrl=", code, StringComparison.Ordinal);
        Assert.Contains("forceLoad: IsQuickAddRoute", code, StringComparison.Ordinal);
        Assert.DoesNotContain("/quick-add", navigation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuickAdd_ReusesAddTransactionFormAndResetsAfterSave()
    {
        var markup = ReadRepoFile("Components/Pages/Checkbook.razor");
        var code = ReadRepoFile("Components/Pages/Checkbook.razor.cs");

        Assert.Contains("currentView == ViewMode.Edit", markup, StringComparison.Ordinal);
        Assert.Contains("ShowAddTransaction();", code, StringComparison.Ordinal);
        Assert.Contains("ResetAddTransactionForm();", code, StringComparison.Ordinal);
        Assert.Contains("await DialogService.Alert(", code, StringComparison.Ordinal);
        Assert.Contains("\"Transaction added.\"", code, StringComparison.Ordinal);
        Assert.Contains("OkButtonText = \"Okay\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Checkbook_RouteTransitionImmediatelyLeavesQuickAddForm()
    {
        var code = ReadRepoFile("Components/Pages/Checkbook.razor.cs");
        var layout = ReadRepoFile("Components/Layout/MainLayout.razor");
        var layoutCode = ReadRepoFile("Components/Layout/MainLayout.razor.cs");

        Assert.Contains("Navigation.LocationChanged += OnLocationChanged;", code, StringComparison.Ordinal);
        Assert.Contains("private void OnLocationChanged(", code, StringComparison.Ordinal);
        Assert.Contains("else if (!isQuickAddRoute)", code, StringComparison.Ordinal);
        Assert.Contains("currentView = ViewMode.List;", code, StringComparison.Ordinal);
        Assert.Contains("StateHasChanged();", code, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged -= OnLocationChanged;", code, StringComparison.Ordinal);
        Assert.Contains("@onclick=\"NavigateToCheckbook\"", layout, StringComparison.Ordinal);
        Assert.Contains("@onclick:preventDefault=\"true\"", layout, StringComparison.Ordinal);
        Assert.Contains("Navigation.NavigateTo(\"/checkbook\", forceLoad: true);", layoutCode, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
