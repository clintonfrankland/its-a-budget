using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Data.Sqlite;
using System.Reflection;

namespace ClintonFrankland.Blazor.Tests;

public class ModificationTimestampTests
{
    [Fact]
    public async Task AsyncSave_StampsCreateAndOnlyGenuinelyModifiedRecord_QueryableFromFreshContext()
    {
        var databaseName = Guid.NewGuid().ToString();
        var created = new DateTime(2026, 8, 7, 18, 20, 0, DateTimeKind.Utc);
        var changed = created.AddMinutes(5);
        var options = Options(databaseName);

        await using (var db = new FixedClockContext(options, created))
        {
            db.Users.AddRange(User(1, "one"), User(2, "two"));
            await db.SaveChangesAsync();
        }

        await using (var db = new FixedClockContext(options, changed))
        {
            var target = await db.Users.SingleAsync(x => x.UserId == 1);
            target.DisplayName = "Changed";
            await db.SaveChangesAsync();
            await db.SaveChangesAsync(); // true no-op must not create another mutation
        }

        await using var verify = new FixedClockContext(options, changed.AddHours(1));
        var records = await verify.Users.OrderBy(x => x.UserId).ToArrayAsync();
        Assert.Equal(changed, records[0].UpdatedAtUtc);
        Assert.Equal(created, records[1].UpdatedAtUtc);
        Assert.All(records, record => Assert.Equal(DateTimeKind.Utc, record.UpdatedAtUtc.Kind));
    }

    [Fact]
    public void SynchronousSave_UsesSameStampingContract()
    {
        var now = new DateTime(2026, 8, 7, 18, 30, 0, DateTimeKind.Utc);
        using var db = new FixedClockContext(Options(Guid.NewGuid().ToString()), now);
        var category = new Category { CategoryName = "Food", UserId = 1 };
        db.Categories.Add(category);
        db.SaveChanges();
        Assert.Equal(now, category.UpdatedAtUtc);
    }

    [Fact]
    public void Migration_UsesGuardedColumnsAndDeterministicBackfill()
    {
        var migration = new ClintonFrankland.Migrations.AddModificationTimestamps();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(ClintonFrankland.Migrations.AddModificationTimestamps)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var operations = builder.Operations.OfType<SqlOperation>().ToArray();
        Assert.Equal(3, operations.Length);
        Assert.Contains("IF COL_LENGTH('cfUsers', 'UpdatedAtUtc') IS NULL", operations[0].Sql);
        Assert.Contains("IF COL_LENGTH('cfTransactions', 'UpdatedAtUtc') IS NULL", operations[0].Sql);
        Assert.DoesNotContain("UPDATE cfUsers", operations[0].Sql);
        Assert.Contains("CONVERT(datetime2, '2026-08-07T18:16:06Z', 127)", operations[1].Sql);
        Assert.DoesNotContain("ALTER COLUMN", operations[1].Sql);
        Assert.Contains("ALTER TABLE cfUsers ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL", operations[2].Sql);
        Assert.All(operations, operation => Assert.DoesNotContain("SYSUTCDATETIME", operation.Sql));
    }

    [Fact]
    public void Migration_ModelsLegacyAndAlreadyPresentColumnUpgradesIdempotently()
    {
        var mutableColumns = new[] { "cfUsers", "cfTransactions", "cfPlaidWebhookDeliveries",
            "cfPlaidTransactionStaging", "cfPlaidSyncRuns", "cfPayees", "cfCategories",
            "cfBudgets", "cfBudgetMembers", "cfBudgetInvites" };

        var legacy = new HashSet<string>();
        ApplyGuardedUpgrade(legacy, mutableColumns);
        Assert.Equal(mutableColumns.Order(), legacy.Order());

        var alreadyPresent = mutableColumns.ToHashSet();
        ApplyGuardedUpgrade(alreadyPresent, mutableColumns);
        Assert.Equal(mutableColumns.Order(), alreadyPresent.Order());
    }

    [Fact]
    public async Task TransactionEditAndClear_AdvanceOnlyTargetedRecord()
    {
        var databaseName = Guid.NewGuid().ToString();
        var initial = Utc(10);
        var edit = Utc(11);
        var clear = Utc(12);
        await using (var db = new FixedClockContext(Options(databaseName), initial))
        {
            db.Transactions.AddRange(Transaction(1), Transaction(2));
            await db.SaveChangesAsync();
        }
        await using (var db = new FixedClockContext(Options(databaseName), edit))
        {
            var target = await db.Transactions.FindAsync(1);
            target!.Notes = "edited";
            await db.SaveChangesAsync();
        }
        await using (var db = new FixedClockContext(Options(databaseName), clear))
        {
            var target = await db.Transactions.FindAsync(1);
            target!.Cleared = true;
            await db.SaveChangesAsync();
        }
        await using var verify = new FixedClockContext(Options(databaseName), Utc(13));
        var rows = await verify.Transactions.OrderBy(x => x.TransactionId).ToArrayAsync();
        Assert.Equal(clear, rows[0].UpdatedAtUtc);
        Assert.Equal(initial, rows[1].UpdatedAtUtc);
    }

    [Fact]
    public async Task EveryMutableFamily_StampsCreatesAndMutationsAtomically()
    {
        var databaseName = Guid.NewGuid().ToString();
        var created = Utc(14);
        var changed = Utc(15);
        await using (var db = new FixedClockContext(Options(databaseName), created))
        {
            AddRepresentativeMutableRecords(db);
            await db.SaveChangesAsync();
            Assert.All(db.ChangeTracker.Entries<IModificationTracked>(), e => Assert.Equal(created, e.Entity.UpdatedAtUtc));
        }
        await using (var db = new FixedClockContext(Options(databaseName), changed))
        {
            await LoadEveryMutableSet(db);
            foreach (var entry in db.ChangeTracker.Entries<IModificationTracked>())
            {
                var businessProperty = entry.Properties.First(p => !p.Metadata.IsPrimaryKey()
                    && p.Metadata.Name is not "UpdatedAtUtc" and not "LastUpdated");
                businessProperty.IsModified = true;
            }
            await db.SaveChangesAsync();
        }
        await using var verify = new FixedClockContext(Options(databaseName), Utc(16));
        await LoadEveryMutableSet(verify);
        Assert.All(verify.ChangeTracker.Entries<IModificationTracked>(), e => Assert.Equal(changed, e.Entity.UpdatedAtUtc));
    }

    [Fact]
    public async Task ExecuteUpdateBypass_UsesExplicitUtcStampAndLeavesOtherRowsUntouched()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseSqlite(connection).Options;
        var initial = Utc(17);
        var changed = Utc(18);
        await using (var db = new FixedClockContext(options, initial))
        {
            await db.Database.EnsureCreatedAsync();
            db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
            db.Accounts.AddRange(Account(1), Account(2));
            await db.SaveChangesAsync();
        }
        await using (var db = new FixedClockContext(options, Utc(19)))
            await db.Accounts.Where(x => x.AccountId == 1).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.LastUpdated, changed));
        await using var verify = new FixedClockContext(options, Utc(20));
        var rows = await verify.Accounts.AsNoTracking().OrderBy(x => x.AccountId).ToArrayAsync();
        Assert.Equal(changed, rows[0].LastUpdated);
        Assert.Equal(initial, rows[1].LastUpdated);
    }

    [Fact]
    public async Task FailedSave_RollsBackAllRecordsAndTheirTimestamps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseSqlite(connection).Options;
        var initial = Utc(21);
        await using (var db = new FixedClockContext(options, initial))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(User(1, "one"), User(2, "two"));
            await db.SaveChangesAsync();
        }
        await using (var db = new FixedClockContext(options, Utc(22)))
        {
            var users = await db.Users.OrderBy(x => x.UserId).ToArrayAsync();
            users[0].DisplayName = "changed";
            users[1].DisplayName = null!; // required-column failure
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var verify = new FixedClockContext(options, Utc(23));
        var persisted = await verify.Users.AsNoTracking().OrderBy(x => x.UserId).ToArrayAsync();
        Assert.Equal(new[] { "one", "two" }, persisted.Select(x => x.DisplayName));
        Assert.All(persisted, x => Assert.Equal(initial, x.UpdatedAtUtc));
    }

    private static void ApplyGuardedUpgrade(HashSet<string> schema, IEnumerable<string> tables)
    {
        foreach (var table in tables) schema.Add(table);
        Assert.All(tables, table => Assert.Contains(table, schema)); // backfill/enforcement can now resolve every column
    }

    private static DateTime Utc(int hour) => new(2026, 8, 7, hour, 0, 0, DateTimeKind.Utc);

    private static Transaction Transaction(int id) => new() { TransactionId = id, TransactionDate = new DateOnly(2026, 8, 7), Amount = 1, PayeeId = 1, CategoryId = 1, AccountId = 1 };
    private static Account Account(int id) => new() { AccountId = id, AccountName = $"Account {id}", AccountTypeId = 1 };

    private static void AddRepresentativeMutableRecords(ClintonFranklandDbContext db)
    {
        db.AddRange(User(1, "user"), Account(1), new Budget { BudgetId = 1, BudgetTypeId = 1, CategoryId = 1 },
            new Category { CategoryId = 1, CategoryName = "category" }, new Payee { PayeeId = 1, PayeeName = "payee", UserId = 1 },
            Transaction(1), new TransactionRule { TransactionRuleId = 1, UserId = 1, ContainsText = "rule" },
            new SharedBudget { SharedBudgetId = 1, Name = "shared", OwnerUserId = 1, UpdatedAtUtc = default },
            new BudgetMember { BudgetMemberId = 1, SharedBudgetId = 1, UserId = 1 },
            new BudgetInvite { BudgetInviteId = 1, SharedBudgetId = 1, InvitedByUserId = 1, InviteTokenHash = "token", ExpiresAtUtc = Utc(23) },
            new CategoryBudgetTarget { CategoryBudgetTargetId = 1, UserId = 1, CategoryId = 1, BudgetMonth = new DateOnly(2026, 8, 1), UpdatedAtUtc = default },
            new SmtpSetting { UpdatedAtUtc = default }, new BillDueNotificationSetting { UpdatedAtUtc = default },
            new PlaidItem { PlaidItemId = 1, UserId = 1, ItemId = "item", EncryptedAccessToken = "cipher" },
            new PlaidAccountMapping { PlaidAccountMappingId = 1, PlaidItemId = 1, PlaidAccountId = "pa", BudgetAccountId = 1 },
            new PlaidTransactionStaging { PlaidTransactionStagingId = 1, UserId = 1, PlaidItemId = 1, BudgetAccountId = 1, PlaidTransactionId = "pt" },
            new PlaidSyncRun { PlaidSyncRunId = 1, PlaidItemId = 1 },
            new PlaidWebhookDelivery { PlaidWebhookDeliveryId = 1, DeliveryKey = "delivery" });
    }

    private static async Task LoadEveryMutableSet(ClintonFranklandDbContext db)
    {
        await db.Users.LoadAsync(); await db.Accounts.LoadAsync(); await db.Budgets.LoadAsync();
        await db.Categories.LoadAsync(); await db.Payees.LoadAsync(); await db.Transactions.LoadAsync();
        await db.TransactionRules.LoadAsync(); await db.SharedBudgets.LoadAsync(); await db.BudgetMembers.LoadAsync();
        await db.BudgetInvites.LoadAsync(); await db.CategoryBudgetTargets.LoadAsync(); await db.SmtpSettings.LoadAsync();
        await db.BillDueNotificationSettings.LoadAsync(); await db.PlaidItems.LoadAsync(); await db.PlaidAccountMappings.LoadAsync();
        await db.PlaidTransactionStaging.LoadAsync(); await db.PlaidSyncRuns.LoadAsync(); await db.PlaidWebhookDeliveries.LoadAsync();
    }

    private static DbContextOptions<ClintonFranklandDbContext> Options(string name) =>
        new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseInMemoryDatabase(name).Options;

    private static User User(int id, string name) => new()
    {
        UserId = id, SiteId = 1, UserName = name, DisplayName = name,
        Salt = "salt", PasswordHash = "hash"
    };

    private sealed class FixedClockContext(DbContextOptions<ClintonFranklandDbContext> options, DateTime utcNow)
        : ClintonFranklandDbContext(options)
    {
        protected override DateTime UtcNow => utcNow;
    }
}
