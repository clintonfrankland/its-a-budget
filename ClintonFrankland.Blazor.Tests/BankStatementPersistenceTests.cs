using System.Data;
using System.Data.Common;
using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClintonFrankland.Blazor.Tests;

public sealed class BankStatementPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedSignedRowsPersistIntoThePreviewedAccountAndRemainReadable(bool legacyAccount)
    {
        await using var fixture = await DatabaseFixture.CreateAsync(legacyAccount: legacyAccount);
        var service = new CheckbookDataService(fixture.Database);
        var destination = await service.GetImportDestinationAsync(42);
        Assert.NotNull(destination);
        Assert.Equal(legacyAccount ? null : (int?)12, destination.SharedBudgetId);

        await service.ImportTransactionsAsync(42,
        [
            new ImportedTransaction(new DateOnly(2026, 9, 1), "Salary", "Income", 1500.125m, true, " paid "),
            new ImportedTransaction(new DateOnly(2026, 9, 2), "Grocer", "Food", -25.555m, false),
            new ImportedTransaction(new DateOnly(2026, 9, 3), "Refund", "Food", 4.50m, true)
        ], destination);

        await using var verify = fixture.CreateContext();
        var rows = await verify.ReadableTransactions(42, [12]).OrderBy(row => row.TransactionDate).ToArrayAsync();
        Assert.Equal(3, rows.Length);
        Assert.Equal(new[] { 1500.13m, -25.56m, 4.50m }, rows.Select(row => row.Amount));
        Assert.Equal(new[] { true, false, true }, rows.Select(row => row.Cleared));
        Assert.Equal(new[] { new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3) }, rows.Select(row => row.TransactionDate));
        Assert.All(rows, row =>
        {
            Assert.Equal(destination.AccountId, row.AccountId);
            Assert.Equal(destination.SharedBudgetId, row.SharedBudgetId);
            Assert.Equal(42, row.UserId);
        });
        Assert.Equal("paid", rows[0].Notes);
        Assert.Equal(2, await verify.Categories.CountAsync());
    }

    [Theory]
    [InlineData("readonly")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    [InlineData("missing")]
    [InlineData("deleted-user")]
    public async Task UnavailableDestinationRejectsTheWholeImportWithoutWrites(string reason)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var database = fixture.Database;
        switch (reason)
        {
            case "readonly": (await database.BudgetMembers.SingleAsync()).Role = BudgetMemberRole.Viewer; break;
            case "foreign":
                var foreignAccount = await database.Accounts.SingleAsync();
                foreignAccount.SharedBudgetId = null;
                foreignAccount.UserId = 99;
                break;
            case "deleted": (await database.Accounts.SingleAsync()).IsDeleted = true; break;
            case "missing": database.Accounts.Remove(await database.Accounts.SingleAsync()); break;
            case "deleted-user": (await database.Users.SingleAsync(user => user.UserId == 42)).IsDeleted = true; break;
        }
        await database.SaveChangesAsync();
        var service = new CheckbookDataService(database);
        Assert.Null(await service.GetImportDestinationAsync(42));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportTransactionsAsync(42, [Row()]));
        await AssertNoImportedRowsAsync(fixture);
        Assert.Empty(database.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("account")]
    [InlineData("active-budget")]
    [InlineData("account-budget")]
    [InlineData("permission")]
    public async Task DestinationChangesAfterPreviewAreRejectedEvenWithTrackedPreviewEntities(string change)
    {
        await using var fixture = await DatabaseFixture.CreateAsync(legacyAccount: change == "active-budget");
        var database = fixture.Database;
        var service = new CheckbookDataService(database);
        var destination = await service.GetImportDestinationAsync(42);
        Assert.NotNull(destination);
        await database.Users.SingleAsync(user => user.UserId == 42);
        await using (var changed = fixture.CreateContext())
        {
            if (change == "account")
            {
                (await changed.Accounts.SingleAsync()).IsDefault = false;
                changed.Accounts.Add(new Account { AccountId = 2, AccountName = "New default", AccountTypeId = 1, UserId = 42, SharedBudgetId = 12, IsDefault = true });
            }
            else if (change == "permission")
                (await changed.BudgetMembers.SingleAsync()).Role = BudgetMemberRole.Viewer;
            else
            {
                changed.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 13, Name = "Other budget", OwnerUserId = 42 });
                changed.BudgetMembers.Add(new BudgetMember { SharedBudgetId = 13, UserId = 42, Role = BudgetMemberRole.Owner });
                await changed.SaveChangesAsync();
                if (change == "active-budget")
                    (await changed.Users.SingleAsync(user => user.UserId == 42)).ActiveSharedBudgetId = 13;
                else
                    (await changed.Accounts.SingleAsync()).SharedBudgetId = 13;
            }
            await changed.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportTransactionsAsync(42, [Row()], destination));
        await AssertNoImportedRowsAsync(fixture);
        Assert.Empty(database.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidLaterRowIsRejectedBeforeTrackingOrSaving(bool invalidDate)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        fixture.Database.ChangeTracker.Clear();
        var service = new CheckbookDataService(fixture.Database);
        var invalidRow = invalidDate
            ? Row() with { TransactionDate = DateOnly.MinValue }
            : Row() with { Amount = 10_000_000m };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportTransactionsAsync(42, [Row(), invalidRow]));
        await AssertNoImportedRowsAsync(fixture);
        Assert.Empty(fixture.Database.ChangeTracker.Entries());
    }

    [Fact]
    public async Task LaterDatabaseFailureRollsBackTransactionsPayeesAndCategories()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await fixture.Database.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectSecondTransaction BEFORE INSERT ON cfTransactions
            WHEN NEW.Notes = 'Reject this row'
            BEGIN
                SELECT RAISE(ABORT, 'simulated later persistence failure');
            END;
            """);
        var service = new CheckbookDataService(fixture.Database);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.ImportTransactionsAsync(42,
            [Row(), Row() with { Notes = "Reject this row", PayeeName = "Another payee", CategoryName = "Another category" }]));
        await AssertNoImportedRowsAsync(fixture);
        Assert.Empty(fixture.Database.ChangeTracker.Entries());
    }

    [Fact]
    public async Task LegacyCategoryLookupDoesNotReuseAnotherUsersCategory()
    {
        await using var fixture = await DatabaseFixture.CreateAsync(legacyAccount: true);
        fixture.Database.Categories.Add(new Category { CategoryName = "Food", UserId = 99 });
        await fixture.Database.SaveChangesAsync();
        await new CheckbookDataService(fixture.Database).ImportTransactionsAsync(42, [Row()]);
        await using var verify = fixture.CreateContext();
        var row = await verify.Transactions.Include(transaction => transaction.Category).SingleAsync();
        Assert.Equal(42, row.Category!.UserId);
        Assert.Null(row.Category.SharedBudgetId);
    }

    [Fact]
    public async Task ImportExplicitlyRequestsAndUsesSerializableIsolation()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var isolationObserver = new IsolationObserver();
        await using var database = fixture.CreateContext(isolationObserver);

        await new CheckbookDataService(database).ImportTransactionsAsync(42, [Row()]);

        Assert.Equal(IsolationLevel.Serializable, Assert.Single(isolationObserver.RequestedIsolationLevels));
        Assert.Equal(IsolationLevel.Serializable, Assert.Single(isolationObserver.ActualIsolationLevels));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingAccountOrPermissionWriterCannotChangeDestinationBetweenResolutionAndCommit(bool changePermission)
    {
        // SQLite verifies real competing writes; this is not a native SQL Server locking test.
        var connectionString = $"Data Source=bank-import-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=1;Pooling=False";
        await using var fixture = await DatabaseFixture.CreateAsync(connectionString: connectionString);
        var saveBarrier = new ImportSaveBarrier();
        await using var database = fixture.CreateContext(saveBarrier);
        var import = new CheckbookDataService(database).ImportTransactionsAsync(42, [Row()]);
        await saveBarrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using var competingConnection = new SqliteConnection(connectionString);
        await competingConnection.OpenAsync();
        await using var command = competingConnection.CreateCommand();
        command.CommandTimeout = 1;
        command.CommandText = changePermission
            ? "UPDATE cfBudgetMembers SET Role = 'Viewer' WHERE UserId = 42 AND SharedBudgetId = 12"
            : "UPDATE cfAccounts SET IsDeleted = 1 WHERE AccountId = 1";
        try
        {
            var failure = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
            Assert.Contains(failure.SqliteErrorCode, new[] { 5, 6 }); // SQLITE_BUSY or SQLITE_LOCKED
        }
        finally
        {
            saveBarrier.Release.TrySetResult();
            await import.WaitAsync(TimeSpan.FromSeconds(5));
        }

        await using (var verify = fixture.CreateContext())
        {
            Assert.Single(await verify.ReadableTransactions(42, [12]).ToArrayAsync());
            Assert.Equal(BudgetMemberRole.Owner, (await verify.BudgetMembers.SingleAsync()).Role);
            Assert.False((await verify.Accounts.SingleAsync()).IsDeleted ?? false);
        }
        // Once import commits, the same writer can proceed: it was blocked by isolation, not invalid SQL.
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private sealed class IsolationObserver : DbTransactionInterceptor
    {
        public List<IsolationLevel> RequestedIsolationLevels { get; } = [];
        public List<IsolationLevel> ActualIsolationLevels { get; } = [];

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            RequestedIsolationLevels.Add(eventData.IsolationLevel);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
            TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
        {
            ActualIsolationLevels.Add(result.IsolationLevel);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ImportSaveBarrier : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Transaction>().Any(entry => entry.State == EntityState.Added))
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private static ImportedTransaction Row() => new(new DateOnly(2026, 9, 1), "Payee", "Food", -12.34m);

    private static async Task AssertNoImportedRowsAsync(DatabaseFixture fixture)
    {
        await using var verify = fixture.CreateContext();
        Assert.Empty(await verify.Transactions.ToArrayAsync());
        Assert.Empty(await verify.Payees.ToArrayAsync());
        Assert.Empty(await verify.Categories.ToArrayAsync());
    }

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ClintonFranklandDbContext Database { get; }

        private DatabaseFixture(SqliteConnection connection)
        {
            _connection = connection;
            Database = CreateContext();
        }

        public ClintonFranklandDbContext CreateContext(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options);

        public static async Task<DatabaseFixture> CreateAsync(bool legacyAccount = false, string connectionString = "Data Source=:memory:")
        {
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var fixture = new DatabaseFixture(connection);
            var database = fixture.Database;
            await database.Database.EnsureCreatedAsync();
            database.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
            database.Users.AddRange(User(42), User(99));
            await database.SaveChangesAsync();
            database.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 12, Name = "Household", OwnerUserId = 42 });
            await database.SaveChangesAsync();
            database.BudgetMembers.Add(new BudgetMember { SharedBudgetId = 12, UserId = 42, Role = BudgetMemberRole.Owner });
            (await database.Users.SingleAsync(user => user.UserId == 42)).ActiveSharedBudgetId = 12;
            database.Accounts.Add(new Account { AccountId = 1, AccountName = "Checking", AccountTypeId = 1, UserId = 42, SharedBudgetId = legacyAccount ? null : 12, IsDefault = true });
            await database.SaveChangesAsync();
            return fixture;
        }

        private static User User(int userId) => new() { UserId = userId, SiteId = 1, UserName = $"user-{userId}", Salt = "salt", PasswordHash = "hash", FirstLogin = DateTime.UtcNow, LastLogin = DateTime.UtcNow };

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
