using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations;

[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260728135000_AddPlaidReconciliationReviewState")]
public partial class AddPlaidReconciliationReviewState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
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
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_cfPlaidTransactionStaging_LinkedTransactionId' AND object_id = OBJECT_ID('cfPlaidTransactionStaging'))
    CREATE UNIQUE INDEX UX_cfPlaidTransactionStaging_LinkedTransactionId ON cfPlaidTransactionStaging(LinkedTransactionId) WHERE LinkedTransactionId IS NOT NULL;");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_cfPlaidTransactionStaging_LinkedTransactionId' AND object_id = OBJECT_ID('cfPlaidTransactionStaging'))
    DROP INDEX UX_cfPlaidTransactionStaging_LinkedTransactionId ON cfPlaidTransactionStaging;
IF COL_LENGTH('cfPlaidTransactionStaging', 'RowVersion') IS NOT NULL ALTER TABLE cfPlaidTransactionStaging DROP COLUMN RowVersion;
IF COL_LENGTH('cfPlaidTransactionStaging', 'ReviewedAtUtc') IS NOT NULL ALTER TABLE cfPlaidTransactionStaging DROP COLUMN ReviewedAtUtc;
IF COL_LENGTH('cfPlaidTransactionStaging', 'LinkedSourceFingerprint') IS NOT NULL ALTER TABLE cfPlaidTransactionStaging DROP COLUMN LinkedSourceFingerprint;
IF COL_LENGTH('cfPlaidTransactionStaging', 'LinkedTransactionId') IS NOT NULL ALTER TABLE cfPlaidTransactionStaging DROP COLUMN LinkedTransactionId;
IF COL_LENGTH('cfPlaidTransactionStaging', 'ReviewState') IS NOT NULL ALTER TABLE cfPlaidTransactionStaging DROP COLUMN ReviewState;");
}
