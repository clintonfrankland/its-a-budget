using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class BudgetItemsDataServiceTests
{
    [Fact]
    public async Task GetBudgetsForUserAsync_ReturnsOnlyRequestedUserBudgets_WithExportNavigationData()
    {
        await using var db = CreateDbContext();
        var housing = new Category { CategoryId = 1, CategoryName = "Housing", UserId = 42 };
        var other = new Category { CategoryId = 2, CategoryName = "Other", UserId = 99 };
        var monthly = new Frequency { FrequencyId = 4, FrequencyName = "Monthly", Sort = 1 };
        var payee = new Payee { PayeeId = 3, PayeeName = "Power Co", UserId = 42, IsDeleted = false };

        db.Categories.AddRange(housing, other);
        db.Frequencies.Add(monthly);
        db.Payees.Add(payee);
        db.Budgets.AddRange(
            new Budget
            {
                BudgetId = 11,
                BudgetName = "Electric",
                BudgetTypeId = 1,
                FrequencyId = monthly.FrequencyId,
                CategoryId = housing.CategoryId,
                PayeeId = payee.PayeeId,
                UserId = 42,
                Amount = 100m
            },
            new Budget
            {
                BudgetId = 12,
                BudgetName = "Other user's budget",
                BudgetTypeId = 1,
                FrequencyId = monthly.FrequencyId,
                CategoryId = other.CategoryId,
                UserId = 99,
                Amount = 200m
            });
        await db.SaveChangesAsync();

        var budgets = await new BudgetItemsDataService(db).GetBudgetsForUserAsync(42);

        var budget = Assert.Single(budgets);
        Assert.Equal("Electric", budget.BudgetName);
        Assert.Equal("Housing", budget.Category?.CategoryName);
        Assert.Equal("Monthly", budget.Frequency?.FrequencyName);
        Assert.Equal("Power Co", budget.Payee?.PayeeName);
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }
}
