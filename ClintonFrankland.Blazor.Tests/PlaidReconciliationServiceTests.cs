using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidReconciliationServiceTests
{
    [Fact]
    public async Task ExactUniqueMatch_IsHighConfidence_AndDoesNotMutateLedger()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, amount: 42.50m, name: "Corner Market");
        AddLedger(database, amount: -42.50m, payeeName: "Corner Market");
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.HighConfidence, result.Disposition);
        Assert.True(Assert.Single(result.Candidates).Confidence >= 90);
        Assert.Empty(result.RejectionReasons);
        Assert.All(database.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        Assert.False((await database.Transactions.SingleAsync()).Cleared);
    }

    [Fact]
    public async Task ExistingPlaidLink_IsHighestTierEvidence()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, plaidTransactionId: "posted-link", amount: 4m, name: "Different name");
        AddLedger(database, amount: -4m, payeeName: "Other", notes: "plaid:posted-link");
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.HighConfidence, result.Disposition);
        Assert.Contains("existing-plaid-link", Assert.Single(result.Candidates).Evidence);
    }

    [Fact]
    public async Task RoundedAmountAndBoundedDate_IsProbable()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, amount: 10m, name: "Coffee House");
        AddLedger(database, amount: -10.01m, payeeName: "Coffee House", date: new DateOnly(2026, 7, 31));
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.Probable, result.Disposition);
        Assert.Contains("amount-rounded", Assert.Single(result.Candidates).Evidence);
        Assert.Contains("date-window", Assert.Single(result.Candidates).Evidence);
    }

    [Fact]
    public async Task DuplicateExactAmounts_AreAmbiguous()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, amount: 20m, name: "Store");
        AddLedger(database, amount: -20m, payeeName: "Store");
        AddLedger(database, amount: -20m, payeeName: "Store", date: new DateOnly(2026, 7, 28));
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.Ambiguous, result.Disposition);
        Assert.Contains("Duplicate amount", Assert.Single(result.RejectionReasons));
    }

    [Fact]
    public async Task PendingAndRemovedEvidence_AreIneligible()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, plaidTransactionId: "pending", isPending: true);
        AddStaged(database, plaidTransactionId: "removed", isRemoved: true);
        await database.SaveChangesAsync();

        var results = await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None);

        Assert.All(results, result => Assert.Equal(PlaidReconciliationDisposition.Ineligible, result.Disposition));
        Assert.Contains("Pending", results[0].RejectionReasons[0]);
        Assert.Contains("removed", results[1].RejectionReasons[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransferEvidence_IsAmbiguous()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, amount: 50m, name: "Transfer to savings");
        AddLedger(database, amount: -50m, payeeName: "Transfer to savings");
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.Ambiguous, result.Disposition);
    }

    [Fact]
    public async Task WrongSign_IsRejected()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, amount: 30m, name: "Market");
        AddLedger(database, amount: 30m, payeeName: "Market");
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.NoMatch, result.Disposition);
        Assert.Contains("sign-correct", Assert.Single(result.Candidates).RejectionReasons[0]);
    }

    [Fact]
    public async Task DateOutsideBoundedWindow_IsNotACandidate()
    {
        await using var database = CreateDatabase();
        AddOwnedAccount(database);
        AddStaged(database, amount: 30m, name: "Market");
        AddLedger(database, amount: -30m, payeeName: "Market", date: new DateOnly(2026, 8, 3));
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.NoMatch, result.Disposition);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task SharedOrUnauthorizedAccount_IsNeverMatched()
    {
        await using var database = CreateDatabase();
        database.Accounts.Add(new Account { AccountId = 10, AccountName = "Shared", UserId = 1, SharedBudgetId = 7 });
        AddStaged(database, amount: 12m, name: "Store");
        AddLedger(database, amount: -12m, payeeName: "Store");
        await database.SaveChangesAsync();

        var result = Assert.Single(await new PlaidReconciliationService(database).ReconcileAsync(1, CancellationToken.None));

        Assert.Equal(PlaidReconciliationDisposition.Ineligible, result.Disposition);
        Assert.Empty(result.Candidates);
    }

    private static ClintonFranklandDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ClintonFranklandDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void AddOwnedAccount(ClintonFranklandDbContext database) =>
        database.Accounts.Add(new Account { AccountId = 10, AccountName = "Checking", UserId = 1 });

    private static void AddStaged(ClintonFranklandDbContext database, string plaidTransactionId = "staged", decimal amount = 10m,
        string name = "Store", bool isPending = false, bool isRemoved = false) =>
        database.PlaidTransactionStaging.Add(new PlaidTransactionStaging
        {
            PlaidTransactionStagingId = database.PlaidTransactionStaging.Local.Count + 1,
            UserId = 1,
            PlaidItemId = 1,
            BudgetAccountId = 10,
            PlaidTransactionId = plaidTransactionId,
            PlaidAccountId = "account",
            PlaidAmount = amount,
            TransactionDate = new DateOnly(2026, 7, 27),
            Name = name,
            IsPending = isPending,
            IsRemoved = isRemoved
        });

    private static void AddLedger(ClintonFranklandDbContext database, decimal amount, string payeeName, string? notes = null,
        DateOnly? date = null) => database.Transactions.Add(new Transaction
        {
            TransactionId = database.Transactions.Local.Count + 1,
            TransactionDate = date ?? new DateOnly(2026, 7, 27),
            Amount = amount,
            AccountId = 10,
            UserId = 1,
            PayeeId = database.Payees.Local.Count + 1,
            CategoryId = 1,
            Payee = new Payee { PayeeId = database.Payees.Local.Count + 1, UserId = 1, PayeeName = payeeName },
            Notes = notes,
            Cleared = false
        });
}
