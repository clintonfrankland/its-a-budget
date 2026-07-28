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
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.Ordinal);
    }
}
