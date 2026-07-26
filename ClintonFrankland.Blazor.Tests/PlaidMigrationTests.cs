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
}
