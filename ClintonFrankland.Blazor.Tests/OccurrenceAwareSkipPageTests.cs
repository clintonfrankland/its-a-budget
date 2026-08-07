namespace ClintonFrankland.Blazor.Tests;

public class OccurrenceAwareSkipPageTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void BudgetForecast_LeftAndRightActionsForwardSelectedOccurrence()
    {
        var markup = ReadRepoFile("Components/Pages/Budget.razor");
        var code = ReadRepoFile("Components/Pages/Budget.razor.cs");

        Assert.Equal(2, Count(markup, "OnSkip=\"@(() => SkipOccurrenceAsync(item))\""));
        Assert.Contains("BudgetSchedule.SkipOccurrenceAsync(CurrentUser.UserId, item.BudgetId, item.DueDate)", code);
        Assert.DoesNotContain("BudgetSchedule.MarkBudgetPaidAsync", code);
    }

    [Fact]
    public void Checkbook_LeftAndRightActionsForwardSelectedOccurrence()
    {
        var markup = ReadRepoFile("Components/Pages/Checkbook.razor");
        var code = ReadRepoFile("Components/Pages/Checkbook.razor.cs");

        Assert.Equal(2, Count(markup, "OnSkip=\"@(() => SkipBudgetAsync(item))\""));
        Assert.Contains("BudgetSchedule.SkipOccurrenceAsync(CurrentUser.UserId, item.BudgetId, item.DueDate)", code);
        Assert.DoesNotContain("SkipBudgetAsync(item.BudgetId)", markup + code);
    }

    [Fact]
    public void Checkbook_IconOnlyTransactionEditActionsHaveAccessibleNames()
    {
        var markup = ReadRepoFile("Components/Pages/Checkbook.razor");
        const string editAction = "Click=\"() => ShowEditTransactionAsync(txn.TransactionId)\" title=\"Edit\" aria-label=\"Edit transaction\"";

        Assert.Equal(2, Count(markup, editAction));
    }

    [Fact]
    public void Checkbook_BalanceBarShowsCanonicalSafeToSpendValueAndDate()
    {
        var markup = ReadRepoFile("Components/Pages/Checkbook.razor");
        var code = ReadRepoFile("Components/Pages/Checkbook.razor.cs");
        var balanceBar = ReadRepoFile("Components/BalanceBar.razor");

        Assert.Contains("<BalanceBar", markup);
        Assert.Contains("Safe to Spend:", balanceBar);
        Assert.Contains("@SafeToSpend.ToString(\"C\")", balanceBar);
        Assert.Contains("@SafeToSpendDate.ToString(\"MMM d\")", balanceBar);
        Assert.Contains("BalanceSummary.GetAsync", code);
    }

    private static string ReadRepoFile(string relativePath) => File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
