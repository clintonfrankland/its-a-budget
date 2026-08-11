using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

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

        Assert.True(LedgerScope.BelongsToAccount(new Transaction { AccountId = 5, SharedBudgetId = 12, UserId = 42 }, account));
        Assert.False(LedgerScope.BelongsToAccount(new Transaction { AccountId = 5, SharedBudgetId = null }, account));
        Assert.False(LedgerScope.BelongsToAccount(new Transaction { AccountId = 5, SharedBudgetId = 13 }, account));
    }

    [Fact]
    public void BelongsToAccount_QuarantinesRemovedSharedBudgetMemberAttribution()
    {
        var account = new Account { AccountId = 5, SharedBudgetId = 12 };
        var transaction = new Transaction { AccountId = 5, SharedBudgetId = 12, UserId = 99 };

        Assert.False(LedgerScope.BelongsToAccount(transaction, account, new HashSet<int> { 42 }));
        Assert.True(LedgerScope.BelongsToAccount(transaction, account, new HashSet<int> { 42, 99 }));
    }

    [Fact]
    public async Task ReadableTransactions_QuarantinesRowsAttributedToRemovedSharedBudgetMembers()
    {
        await using var db = new ClintonFranklandDbContext(new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Accounts.Add(new Account { AccountId = 5, UserId = 42, SharedBudgetId = 12 });
        db.BudgetMembers.AddRange(
            new BudgetMember { BudgetMemberId = 1, SharedBudgetId = 12, UserId = 42, Role = BudgetMemberRole.Owner, Status = BudgetMemberStatus.Active },
            new BudgetMember { BudgetMemberId = 2, SharedBudgetId = 12, UserId = 99, Role = BudgetMemberRole.Owner, Status = BudgetMemberStatus.Removed });
        db.Transactions.AddRange(
            new Transaction { TransactionId = 1, AccountId = 5, UserId = 42, SharedBudgetId = 12, Amount = -10m },
            new Transaction { TransactionId = 2, AccountId = 5, UserId = 99, SharedBudgetId = 12, Amount = -365m });
        await db.SaveChangesAsync();

        var transactions = await db.ReadableTransactions(42, [12]).ToListAsync();

        Assert.Collection(transactions, transaction => Assert.Equal(1, transaction.TransactionId));
    }
}
