using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizePlaidModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The preceding guarded Plaid migrations already apply this schema.
            // This migration advances EF's model snapshot so startup can safely
            // discover and execute those migrations.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Snapshot-only migration. The guarded Plaid schema is retained.
        }
    }
}
