using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations;

[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260728135000_AddPlaidReconciliationReviewState")]
public partial class AddPlaidReconciliationReviewState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep schema changes and dependent index creation in separate batches. SQL Server
        // compiles a batch before executing it, so it cannot safely compile the index until
        // LinkedTransactionId exists.
        migrationBuilder.Sql(@"
IF OBJECT_ID('cfPlaidTransactionStaging', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('cfPlaidTransactionStaging', 'ReviewState') IS NULL
        ALTER TABLE cfPlaidTransactionStaging ADD ReviewState NVARCHAR(24) NOT NULL CONSTRAINT DF_cfPlaidTransactionStaging_ReviewState DEFAULT 'pending';
    IF COL_LENGTH('cfPlaidTransactionStaging', 'LinkedTransactionId') IS NULL
        ALTER TABLE cfPlaidTransactionStaging ADD LinkedTransactionId INT NULL;
    IF COL_LENGTH('cfPlaidTransactionStaging', 'LinkedSourceFingerprint') IS NULL
        ALTER TABLE cfPlaidTransactionStaging ADD LinkedSourceFingerprint NVARCHAR(128) NULL;
    IF COL_LENGTH('cfPlaidTransactionStaging', 'ReviewedAtUtc') IS NULL
        ALTER TABLE cfPlaidTransactionStaging ADD ReviewedAtUtc DATETIME2 NULL;
    IF COL_LENGTH('cfPlaidTransactionStaging', 'RowVersion') IS NULL
        ALTER TABLE cfPlaidTransactionStaging ADD RowVersion ROWVERSION NOT NULL;

END;");

        migrationBuilder.Sql(@"
IF OBJECT_ID('cfPlaidTransactionStaging', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_cfPlaidTransactionStaging_LinkedTransactionId' AND object_id = OBJECT_ID('cfPlaidTransactionStaging'))
    BEGIN
        IF EXISTS (SELECT LinkedTransactionId FROM cfPlaidTransactionStaging WHERE LinkedTransactionId IS NOT NULL GROUP BY LinkedTransactionId HAVING COUNT(*) > 1)
            THROW 51000, 'Cannot create Plaid reconciliation link index because duplicate ledger links already exist.', 1;
        CREATE UNIQUE INDEX UX_cfPlaidTransactionStaging_LinkedTransactionId ON cfPlaidTransactionStaging(LinkedTransactionId) WHERE LinkedTransactionId IS NOT NULL;
    END
END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
IF OBJECT_ID('cfPlaidTransactionStaging', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_cfPlaidTransactionStaging_LinkedTransactionId' AND object_id = OBJECT_ID('cfPlaidTransactionStaging'))
        DROP INDEX UX_cfPlaidTransactionStaging_LinkedTransactionId ON cfPlaidTransactionStaging;
END;");
}
