using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddModificationTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server compiles a batch before executing it. Keep schema creation,
            // references to the new columns, and nullability enforcement in distinct
            // commands so a legacy database can add the columns on its first upgrade.
            migrationBuilder.Sql("""
                IF COL_LENGTH('cfUsers', 'UpdatedAtUtc') IS NULL ALTER TABLE cfUsers ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfTransactions', 'UpdatedAtUtc') IS NULL ALTER TABLE cfTransactions ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfPlaidWebhookDeliveries', 'UpdatedAtUtc') IS NULL ALTER TABLE cfPlaidWebhookDeliveries ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfPlaidTransactionStaging', 'UpdatedAtUtc') IS NULL ALTER TABLE cfPlaidTransactionStaging ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfPlaidSyncRuns', 'UpdatedAtUtc') IS NULL ALTER TABLE cfPlaidSyncRuns ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfPayees', 'UpdatedAtUtc') IS NULL ALTER TABLE cfPayees ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfCategories', 'UpdatedAtUtc') IS NULL ALTER TABLE cfCategories ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfBudgets', 'UpdatedAtUtc') IS NULL ALTER TABLE cfBudgets ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfBudgetMembers', 'UpdatedAtUtc') IS NULL ALTER TABLE cfBudgetMembers ADD UpdatedAtUtc datetime2 NULL;
                IF COL_LENGTH('cfBudgetInvites', 'UpdatedAtUtc') IS NULL ALTER TABLE cfBudgetInvites ADD UpdatedAtUtc datetime2 NULL;
                """);

            migrationBuilder.Sql("""
                DECLARE @backfill datetime2 = CONVERT(datetime2, '2026-08-07T18:16:06Z', 127);
                UPDATE cfUsers SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfTransactions SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfPlaidWebhookDeliveries SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfPlaidTransactionStaging SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfPlaidSyncRuns SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfPayees SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfCategories SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfBudgets SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfBudgetMembers SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfBudgetInvites SET UpdatedAtUtc = @backfill WHERE UpdatedAtUtc IS NULL;
                UPDATE cfAccounts SET LastUpdated = @backfill WHERE LastUpdated IS NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE cfUsers ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfTransactions ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfPlaidWebhookDeliveries ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfPlaidTransactionStaging ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfPlaidSyncRuns ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfPayees ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfCategories ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfBudgets ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfBudgetMembers ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfBudgetInvites ALTER COLUMN UpdatedAtUtc datetime2 NOT NULL;
                ALTER TABLE cfAccounts ALTER COLUMN LastUpdated datetime2 NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: modification metadata is retained on rollback to avoid data loss.
        }
    }
}
