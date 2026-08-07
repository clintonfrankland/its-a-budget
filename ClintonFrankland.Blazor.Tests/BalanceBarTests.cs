using Bunit;
using ClintonFrankland.Components;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public sealed class BalanceBarTests
{
    [Fact]
    public void MonthlyPlan_NormalizesIncomeAndExpenses()
    {
        var budgets = new[]
        {
            new Budget { Amount = 4000m, FrequencyId = 4, BudgetTypeId = 0 },
            new Budget { Amount = 120m, FrequencyId = 1, BudgetTypeId = 1 },
            new Budget { Amount = 1200m, FrequencyId = 9, BudgetTypeId = 1 }
        };

        var monthlyPlan = budgets.Sum(BalanceSummaryService.CalculateMonthlyAmount);

        Assert.Equal(3380m, monthlyPlan);
    }

    [Fact]
    public void SharedBalanceBar_RendersAllFourMeasures()
    {
        using var context = new BunitContext();
        var component = context.Render<BalanceBar>(parameters => parameters
            .Add(p => p.Balance, 100m)
            .Add(p => p.ClearedBalance, 90m)
            .Add(p => p.SafeToSpend, 40m)
            .Add(p => p.SafeToSpendDate, new DateTime(2026, 9, 1))
            .Add(p => p.MonthlyPlan, -25m));

        Assert.Contains("Balance:", component.Markup);
        Assert.Contains("Cleared:", component.Markup);
        Assert.Contains("Safe to Spend:", component.Markup);
        Assert.Contains("Monthly Plan:", component.Markup);
        Assert.Contains("Sep 1", component.Markup);
        Assert.Contains("text-danger", component.Markup);
    }

    [Fact]
    public void CheckbookForecastAndBudgetItems_UseTheSharedBalanceBar()
    {
        var root = FindRepositoryRoot();
        foreach (var path in new[]
        {
            "Components/Pages/Checkbook.razor",
            "Components/Pages/Budget.razor",
            "Components/Pages/BudgetItems.razor"
        })
        {
            Assert.Contains("<BalanceBar", File.ReadAllText(Path.Combine(root, path)));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ClintonFrankland.Blazor.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
