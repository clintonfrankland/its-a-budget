using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class BudgetAllowanceServiceTests
{
    [Fact]
    public async Task CurrentProgress_ReducesWeeklyAllowanceByPostedCategoryExpenses()
    {
        await using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var reset = today.AddDays(3);
        Seed(db, reset);
        db.Transactions.AddRange(
            Transaction(1, today.AddDays(-2), -40m),
            Transaction(2, today.AddDays(-5), -20m),
            Transaction(3, today.AddDays(-1), 500m));
        await db.SaveChangesAsync();

        var budget = await db.Budgets.SingleAsync(b => b.IsSpendingAllowance);
        var progress = (await new BudgetAllowanceService(db).GetCurrentProgressAsync(42, [budget], today))[budget.BudgetId];

        Assert.Equal(150m, progress.PlannedAmount);
        Assert.Equal(40m, progress.SpentAmount);
        Assert.Equal(110m, progress.RemainingAmount);
        Assert.Equal(reset, progress.PeriodEnd);
    }

    [Fact]
    public async Task Forecast_ReducesAllowanceOnlyByPostedTransactions()
    {
        await using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var reset = today.AddDays(3);
        Seed(db, reset);
        db.Transactions.Add(Transaction(1, today.AddDays(-2), -40m));
        db.Budgets.Add(new Budget
        {
            BudgetId = 2,
            BudgetName = "Grocery pickup",
            BudgetTypeId = 1,
            FrequencyId = 0,
            NextDueDate = today.AddDays(1).ToDateTime(TimeOnly.MinValue),
            EndDate = new DateTime(1970, 1, 1),
            Amount = 25m,
            CategoryId = 1,
            UserId = 42
        });
        await db.SaveChangesAsync();

        var forecast = await new BudgetScheduleService(db).GetForecastAsync(
            42,
            reset.ToDateTime(TimeOnly.MinValue),
            includeEndDate: true);

        var scheduled = Assert.Single(forecast, i => i.BudgetId == 2);
        var allowance = Assert.Single(forecast, i => i.IsSpendingAllowance);
        Assert.Equal(-25m, scheduled.Amount);
        Assert.Equal(-110m, allowance.Amount);
        Assert.Equal(150m, allowance.PlannedAmount);
        Assert.Equal(40m, allowance.SpentAmount);
        Assert.Equal(110m, allowance.RemainingAmount);
        Assert.Equal(825m, allowance.Balance);
    }

    [Fact]
    public async Task AllowanceCannotBeRecordedOrSkippedAsACheckbookTransaction()
    {
        await using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        Seed(db, today.AddDays(3));
        await db.SaveChangesAsync();
        var service = new BudgetScheduleService(db);

        Assert.False(await service.RecordOccurrenceToCheckbookAsync(42, 1, today.AddDays(3).ToDateTime(TimeOnly.MinValue)));
        Assert.False(await service.SkipOccurrenceAsync(42, 1, today.AddDays(3).ToDateTime(TimeOnly.MinValue)));
        Assert.Empty(db.Transactions);
    }

    [Fact]
    public async Task ExpiredAllowance_HasNoCurrentProgressOrFutureForecast()
    {
        await using var db = CreateDbContext();
        var today = new DateOnly(2026, 7, 15);
        Seed(db, new DateOnly(2026, 7, 10));
        var budget = db.Budgets.Local.Single();
        budget.EndDate = today.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        await db.SaveChangesAsync();

        var progress = await new BudgetAllowanceService(db).GetCurrentProgressAsync(42, [budget], today);
        var forecast = await new BudgetScheduleService(db).GetForecastAsync(
            42,
            today.ToDateTime(TimeOnly.MinValue),
            today.AddDays(30).ToDateTime(TimeOnly.MinValue),
            includeEndDate: true);

        Assert.Empty(progress);
        Assert.DoesNotContain(forecast, item => item.IsSpendingAllowance);
    }

    [Fact]
    public void SemiMonthlyAllowance_NormalizesLegacyAnchorAndUsesFirstAndFifteenthBoundaries()
    {
        var budget = new Budget
        {
            IsSpendingAllowance = true,
            FrequencyId = 8,
            NextDueDate = new DateTime(2026, 7, 10)
        };

        Assert.Equal(
            (new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)),
            BudgetAllowanceService.GetCurrentPeriod(budget, new DateOnly(2026, 7, 10)));
        Assert.Equal(
            (new DateOnly(2026, 7, 15), new DateOnly(2026, 8, 1)),
            BudgetAllowanceService.GetCurrentPeriod(budget, new DateOnly(2026, 7, 15)));
    }

    [Fact]
    public void AllowanceOverlap_AllowsReplacementAfterPriorAllowanceEnds()
    {
        var prior = new Budget
        {
            IsSpendingAllowance = true,
            FrequencyId = 1,
            NextDueDate = new DateTime(2026, 7, 8),
            EndDate = new DateTime(2026, 7, 31)
        };
        var replacement = new Budget
        {
            IsSpendingAllowance = true,
            FrequencyId = 1,
            NextDueDate = new DateTime(2026, 8, 8),
            EndDate = new DateTime(1970, 1, 1)
        };

        Assert.False(BudgetAllowanceService.Overlaps(prior, replacement));
    }

    private static void Seed(ClintonFranklandDbContext db, DateOnly reset)
    {
        db.Users.Add(new User { UserId = 42, UserName = "owner", DisplayName = "Owner" });
        db.Accounts.Add(new Account { AccountId = 1, AccountName = "Checking", UserId = 42, BeginningBalance = 1000m, IsDefault = true });
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Groceries", UserId = 42 });
        db.Frequencies.Add(new Frequency { FrequencyId = 1, FrequencyName = "Weekly" });
        db.Payees.Add(new Payee { PayeeId = 1, PayeeName = "Store", UserId = 42 });
        db.Budgets.Add(new Budget
        {
            BudgetId = 1,
            BudgetName = "Groceries",
            BudgetTypeId = 1,
            FrequencyId = 1,
            NextDueDate = reset.ToDateTime(TimeOnly.MinValue),
            EndDate = new DateTime(1970, 1, 1),
            Amount = 150m,
            CategoryId = 1,
            UserId = 42,
            IsSpendingAllowance = true
        });
    }

    private static Transaction Transaction(int id, DateOnly date, decimal amount) => new()
    {
        TransactionId = id,
        TransactionDate = date,
        Amount = amount,
        PayeeId = 1,
        CategoryId = 1,
        AccountId = 1,
        UserId = 42
    };

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ClintonFranklandDbContext(options);
    }
}
