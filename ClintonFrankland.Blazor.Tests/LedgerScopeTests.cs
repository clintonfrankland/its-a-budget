using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public sealed class LedgerScopeTests
{
    [Fact]
    public void BelongsToAccount_QuarantinesProductionShapeLegacyRows()
    {
        var account = new Account { AccountId = 10, UserId = 7, BeginningBalance = 100m };
        var valid = new Transaction { AccountId = 10, UserId = 7, Amount = 25m, Cleared = true };
        var legacy = Enumerable.Range(0, 716).Select(index => new Transaction
        {
            AccountId = 10,
            UserId = index % 2 == 0 ? null : 999,
            SharedBudgetId = index % 3 == 0 ? 44 : null,
            Amount = index < 365 ? 1m : index == 365 ? -290m : 0m,
            Cleared = index < 365
        });

        var visible = legacy.Append(valid).Where(transaction => LedgerScope.BelongsToAccount(transaction, account)).ToList();

        Assert.Single(visible);
        Assert.Equal(25m, visible.Sum(transaction => transaction.Amount));
        Assert.Equal(125m, account.BeginningBalance + visible.Where(transaction => transaction.Cleared).Sum(transaction => transaction.Amount));
        Assert.Equal(75m, legacy.Sum(transaction => transaction.Amount)); // +365 cleared and -290 uncleared must not leak in.
    }

    [Fact]
    public void BelongsToAccount_RequiresExactSharedBudgetMatch()
    {
        var account = new Account { AccountId = 5, SharedBudgetId = 12 };

        Assert.True(LedgerScope.BelongsToAccount(new Transaction { AccountId = 5, SharedBudgetId = 12 }, account));
        Assert.False(LedgerScope.BelongsToAccount(new Transaction { AccountId = 5, SharedBudgetId = null }, account));
        Assert.False(LedgerScope.BelongsToAccount(new Transaction { AccountId = 5, SharedBudgetId = 13 }, account));
    }
}
