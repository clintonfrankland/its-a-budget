using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class OnboardingServiceTests
{
    [Fact]
    public async Task NewUserIsOfferedOnboardingAndExistingUserIsNot()
    {
        await using var database = CreateDatabase();
        database.Users.AddRange(User(1, false), User(2, true));
        await database.SaveChangesAsync();
        var service = CreateService(database);

        Assert.True(await service.ShouldOfferAsync(1));
        Assert.False(await service.ShouldOfferAsync(2));
    }

    [Fact]
    public async Task SavedStepIsReturnedWhenUserResumes()
    {
        await using var database = CreateDatabase();
        database.Users.Add(User(1, false));
        await database.SaveChangesAsync();
        var service = CreateService(database);

        await service.SaveStepAsync(1, OnboardingService.CategoriesStep);

        var resumed = await service.GetStateAsync(1);
        Assert.Equal(OnboardingService.CategoriesStep, resumed!.Step);
        Assert.False(resumed.IsCompleted);
    }

    [Fact]
    public async Task OptionalStepsCanBeSkippedWithoutCreatingRecords()
    {
        await using var database = CreateDatabase();
        database.Users.Add(User(1, false));
        await database.SaveChangesAsync();
        var service = CreateService(database);

        await service.SaveStepAsync(1, OnboardingService.SharingStep);
        await service.SaveStepAsync(1, OnboardingService.ReviewStep);

        Assert.Empty(database.Categories);
        Assert.Empty(database.BudgetInvites);
        Assert.Equal(OnboardingService.ReviewStep, (await service.GetStateAsync(1))!.Step);
    }

    [Fact]
    public async Task AccountSetupCreatesDefaultStartingBalanceWithoutLedgerAdjustment()
    {
        await using var database = CreateDatabase();
        database.Users.Add(User(1, false));
        await database.SaveChangesAsync();
        var service = CreateService(database);
        await service.SaveBudgetAsync(1, null, "My Household");

        var accountId = await service.SaveAccountAsync(1, "Daily checking", 1, 123.45m);

        var account = await database.Accounts.SingleAsync(candidate => candidate.AccountId == accountId);
        Assert.True(account.IsDefault);
        Assert.Equal(123.45m, account.BeginningBalance);
        Assert.Equal(123.45m, account.Balance);
        Assert.Equal(123.45m, account.ClearedBalance);
        Assert.Empty(database.Transactions);
    }

    [Fact]
    public async Task CompletionPersistsAndStopsFutureOffer()
    {
        await using var database = CreateDatabase();
        database.Users.Add(User(1, false));
        await database.SaveChangesAsync();
        var service = CreateService(database);

        await service.CompleteAsync(1);

        Assert.False(await service.ShouldOfferAsync(1));
        var user = await database.Users.SingleAsync();
        Assert.True(user.OnboardingCompleted);
        Assert.Equal(OnboardingService.ReviewStep, user.OnboardingStep);
    }

    [Fact]
    public async Task SharedBudgetMemberCanSelectExistingReadableBudget()
    {
        await using var database = CreateDatabase();
        database.Users.AddRange(User(1, false), User(2, true));
        var budget = new SharedBudget { Name = "Shared home", OwnerUserId = 2 };
        database.SharedBudgets.Add(budget);
        await database.SaveChangesAsync();
        database.BudgetMembers.AddRange(
            new BudgetMember { SharedBudgetId = budget.SharedBudgetId, UserId = 2, Role = BudgetMemberRole.Owner, Status = BudgetMemberStatus.Active },
            new BudgetMember { SharedBudgetId = budget.SharedBudgetId, UserId = 1, Role = BudgetMemberRole.Editor, Status = BudgetMemberStatus.Active });
        await database.SaveChangesAsync();
        var service = CreateService(database);

        var selected = await service.SaveBudgetAsync(1, budget.SharedBudgetId, "Ignored rename");

        Assert.Equal(budget.SharedBudgetId, selected);
        Assert.Equal(budget.SharedBudgetId, (await database.Users.SingleAsync(user => user.UserId == 1)).ActiveSharedBudgetId);
        Assert.Equal("Shared home", (await database.SharedBudgets.SingleAsync()).Name);
    }

    private static OnboardingService CreateService(ClintonFranklandDbContext database)
    {
        var sharedBudgets = new SharedBudgetDataService(database);
        var accounts = new AccountsDataService(database, sharedBudgets);
        var invites = new BudgetInviteService(database, sharedBudgets, TimeProvider.System);
        return new OnboardingService(database, sharedBudgets, accounts, invites);
    }

    private static ClintonFranklandDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ClintonFranklandDbContext(options);
    }

    private static User User(int id, bool onboardingCompleted) => new()
    {
        UserId = id,
        SiteId = 1,
        UserName = $"user{id}",
        DisplayName = $"User {id}",
        Salt = "salt",
        PasswordHash = "hash",
        FirstLogin = DateTime.UtcNow,
        LastLogin = DateTime.UtcNow,
        OnboardingCompleted = onboardingCompleted
    };
}
