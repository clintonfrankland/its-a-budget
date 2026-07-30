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
        Assert.Contains("successMessage = \"Transaction added.\"", code, StringComparison.Ordinal);
        Assert.Contains("ResetAddTransactionForm();", code, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
