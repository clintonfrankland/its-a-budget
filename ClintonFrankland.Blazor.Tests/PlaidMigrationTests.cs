using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidMigrationTests
{
    [Fact]
    public void PlaidFoundationMigration_IsDiscoverableByEfCore()
    {
        var services = new ServiceCollection()
            .AddEntityFrameworkSqlServer()
            .AddDbContext<ClintonFranklandDbContext>(options =>
                options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlaidMigrationDiscovery;Trusted_Connection=True"))
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var migrations = database.GetService<IMigrationsAssembly>();

        Assert.Contains("20260726234500_AddPlaidConnectionFoundation", migrations.Migrations.Keys);
    }

    [Fact]
    public void ReconciliationReviewMigration_IsDiscoverableAndGuardsLegacySchema()
    {
        var services = new ServiceCollection()
            .AddEntityFrameworkSqlServer()
            .AddDbContext<ClintonFranklandDbContext>(options =>
                options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlaidMigrationDiscovery;Trusted_Connection=True"))
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var migrations = database.GetService<IMigrationsAssembly>();
        Assert.Contains("20260728135000_AddPlaidReconciliationReviewState", migrations.Migrations.Keys);
        var migrationPath = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../")), "Migrations", "20260728135000_AddPlaidReconciliationReviewState.cs");
        var sql = File.ReadAllText(migrationPath);

        Assert.Contains("IF OBJECT_ID('cfPlaidTransactionStaging', 'U') IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("IF EXISTS (SELECT LinkedTransactionId", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51000", sql, StringComparison.Ordinal);
        var schemaBatch = sql.IndexOf("migrationBuilder.Sql(@\"", StringComparison.Ordinal);
        var indexBatch = sql.IndexOf("CREATE UNIQUE INDEX UX_cfPlaidTransactionStaging_LinkedTransactionId", StringComparison.Ordinal);
        Assert.True(schemaBatch >= 0 && indexBatch > schemaBatch);
        Assert.Contains("END;\");\n\n        migrationBuilder.Sql(@\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void LearnedRulesMigration_IsDiscoverableAndCreatesIndexInSeparateBatch()
    {
        var services = new ServiceCollection().AddEntityFrameworkSqlServer().AddDbContext<ClintonFranklandDbContext>(options =>
            options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlaidMigrationDiscovery;Trusted_Connection=True")).BuildServiceProvider();
        using var scope = services.CreateScope();
        var migrations = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>().GetService<IMigrationsAssembly>();
        Assert.Contains("20260728154500_AddLearnedPlaidTransactionRules", migrations.Migrations.Keys);
        var migrationPath = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../")), "Migrations", "20260728154500_AddLearnedPlaidTransactionRules.cs");
        var sql = File.ReadAllText(migrationPath);
        Assert.Contains("[Migration(\"20260728154500_AddLearnedPlaidTransactionRules\")]", sql, StringComparison.Ordinal);
        Assert.True(sql.Split("migrationBuilder.Sql", StringSplitOptions.None).Length >= 3, "Schema and index operations must use separate migration batches.");
        Assert.Contains("NOT EXISTS (SELECT 1 FROM sys.indexes", sql, StringComparison.Ordinal);
        Assert.Contains("IF COL_LENGTH('dbo.cfTransactionRules', 'ApprovalState') IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentModel_MatchesLatestMigrationSnapshot()
    {
        var services = new ServiceCollection()
            .AddEntityFrameworkSqlServer()
            .AddDbContext<ClintonFranklandDbContext>(options =>
                options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlaidMigrationDiscovery;Trusted_Connection=True"))
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
        var migrations = database.GetService<IMigrationsAssembly>();

        Assert.Contains("20260729122859_SynchronizePlaidModelSnapshot", migrations.Migrations.Keys);
        Assert.False(database.Database.HasPendingModelChanges());
    }
}
