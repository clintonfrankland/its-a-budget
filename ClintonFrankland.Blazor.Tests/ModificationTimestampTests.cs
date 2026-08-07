using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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

        var sql = Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("IF COL_LENGTH('cfUsers', 'UpdatedAtUtc') IS NULL", sql);
        Assert.Contains("IF COL_LENGTH('cfTransactions', 'UpdatedAtUtc') IS NULL", sql);
        Assert.Contains("CONVERT(datetime2, '2026-08-07T18:16:06Z', 127)", sql);
        Assert.DoesNotContain("SYSUTCDATETIME", sql);
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
