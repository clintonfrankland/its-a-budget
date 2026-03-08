using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations;

/// <inheritdoc />
public partial class InitialBaseline : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Baseline migration.
        // The existing database schema predates EF migrations, so this migration is intentionally empty.
        // It exists to create and seed __EFMigrationsHistory so future migrations can be applied.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty.
    }
}
