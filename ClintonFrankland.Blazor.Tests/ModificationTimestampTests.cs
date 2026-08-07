using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task ApplicationServices_StampAccountBudgetCategoryPayeeRuleAndImportPaths()
    {
        var databaseName = Guid.NewGuid().ToString();
        var created = Utc(14);
        var changed = Utc(15);
        await using (var db = new FixedClockContext(Options(databaseName), created))
        {
            db.Users.Add(User(1, "user"));
            db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
            var account = Account(1);
            account.UserId = 1;
            account.IsDefault = true;
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
        }
        await using (var db = new FixedClockContext(Options(databaseName), changed))
        {
            await new AccountsDataService(db).SaveAccountAsync(1, 1, "Renamed", "1", 1, 0, 0, 0, 1, 0, 0, "", changed);
            await new BudgetItemsDataService(db).SaveBudgetAsync(1, -1, "Rent", 1, 1,
                new DateTime(2026, 9, 1), new DateTime(1970, 1, 1), 100, "Housing", "Landlord", false, true, false);
            var payee = await db.Payees.SingleAsync();
            await new PayeesDataService(db).UpdatePayeeAsync(1, payee.PayeeId, "Property Manager");
            await new TransactionRulesDataService(db).SaveAsync(1,
                new TransactionRuleDraft(null, "rent", null, null, null, "Housing", "Property Manager", null, true));
            await new CheckbookDataService(db).ImportTransactionsAsync(1,
                [new ImportedTransaction(new DateOnly(2026, 8, 7), "Imported", "Imported Category", -10m, true)]);
        }
        await using var verify = new FixedClockContext(Options(databaseName), Utc(16));
        Assert.Equal(changed, (await verify.Accounts.SingleAsync()).LastUpdated);
        Assert.All(await verify.Budgets.ToArrayAsync(), row => Assert.Equal(changed, row.UpdatedAtUtc));
        Assert.All(await verify.Categories.ToArrayAsync(), row => Assert.Equal(changed, row.UpdatedAtUtc));
        Assert.All(await verify.Payees.ToArrayAsync(), row => Assert.Equal(changed, row.UpdatedAtUtc));
        Assert.All(await verify.TransactionRules.ToArrayAsync(), row => Assert.Equal(changed, row.UpdatedAtUtc));
        Assert.All(await verify.Transactions.ToArrayAsync(), row => Assert.Equal(changed, row.UpdatedAtUtc));
    }

    [Fact]
    public async Task BudgetAdvanceAndSkip_StampThroughScheduleService_WhileRejectedOccurrenceIsNoOp()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = new FixedClockContext(Options(name), Utc(14)))
        {
            db.Users.Add(User(1, "owner"));
            db.Budgets.AddRange(
                new Budget { BudgetId = 1, UserId = 1, BudgetTypeId = 1, CategoryId = 1, FrequencyId = 1, NextDueDate = new DateTime(2026, 8, 1) },
                new Budget { BudgetId = 2, UserId = 1, BudgetTypeId = 1, CategoryId = 1, FrequencyId = 1, NextDueDate = new DateTime(2026, 8, 1) });
            await db.SaveChangesAsync();
        }
        await using (var db = new FixedClockContext(Options(name), Utc(15)))
        {
            var service = new BudgetScheduleService(db);
            await service.MarkBudgetPaidAsync(1, 1);
            Assert.False(await service.SkipOccurrenceAsync(1, 2, new DateTime(2026, 7, 1)));
        }
        await using var verify = new FixedClockContext(Options(name), Utc(16));
        var rows = await verify.Budgets.OrderBy(x => x.BudgetId).ToArrayAsync();
        Assert.Equal(Utc(15), rows[0].UpdatedAtUtc);
        Assert.Equal(Utc(14), rows[1].UpdatedAtUtc);
    }

    [Fact]
    public async Task Sharing_AuthorizedMutationStampsMemberAndBudget_UnauthorizedMutationDoesNot()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = new FixedClockContext(Options(name), Utc(14)))
        {
            db.Users.AddRange(User(1, "owner"), User(2, "member"), User(3, "outsider"));
            db.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 1, Name = "Home", OwnerUserId = 1 });
            db.BudgetMembers.AddRange(
                new BudgetMember { BudgetMemberId = 1, SharedBudgetId = 1, UserId = 1, Role = BudgetMemberRole.Owner },
                new BudgetMember { BudgetMemberId = 2, SharedBudgetId = 1, UserId = 2, Role = BudgetMemberRole.Viewer });
            await db.SaveChangesAsync();
        }
        DateTime budgetStamp;
        await using (var db = new FixedClockContext(Options(name), Utc(15)))
        {
            Assert.True(await new SharedBudgetDataService(db).ChangeMemberRoleAsync(1, 2, BudgetMemberRole.Editor));
            budgetStamp = (await db.SharedBudgets.SingleAsync()).UpdatedAtUtc;
        }
        await using (var db = new FixedClockContext(Options(name), Utc(16)))
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                new SharedBudgetDataService(db).ChangeMemberRoleAsync(3, 2, BudgetMemberRole.Viewer));
        await using var verify = new FixedClockContext(Options(name), Utc(17));
        Assert.Equal(Utc(15), (await verify.BudgetMembers.SingleAsync(x => x.BudgetMemberId == 2)).UpdatedAtUtc);
        Assert.Equal(budgetStamp, (await verify.SharedBudgets.SingleAsync()).UpdatedAtUtc);
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

    private static DateTime Utc(int hour) => new(2026, 8, 7, hour, 0, 0, DateTimeKind.Utc);

    private static Transaction Transaction(int id) => new() { TransactionId = id, TransactionDate = new DateOnly(2026, 8, 7), Amount = 1, PayeeId = 1, CategoryId = 1, AccountId = 1 };
    private static Account Account(int id) => new() { AccountId = id, AccountName = $"Account {id}", AccountTypeId = 1 };

    private static DbContextOptions<ClintonFranklandDbContext> Options(string name) =>
        new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

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
