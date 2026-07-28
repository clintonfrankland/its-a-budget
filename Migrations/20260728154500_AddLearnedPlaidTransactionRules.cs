using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ClintonFrankland.Data;

namespace ClintonFrankland.Migrations;

/// <summary>Adds explainable, user-approved learning metadata to the existing cfTransactionRules engine.</summary>
[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260728154500_AddLearnedPlaidTransactionRules")]
public partial class AddLearnedPlaidTransactionRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.cfPlaidTransactionStaging', 'MerchantEntityId') IS NULL
    ALTER TABLE dbo.cfPlaidTransactionStaging ADD MerchantEntityId nvarchar(128) NULL;
IF COL_LENGTH('dbo.cfTransactionRules', 'MerchantEntityId') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD MerchantEntityId nvarchar(128) NULL;
IF COL_LENGTH('dbo.cfTransactionRules', 'NormalizedMerchant') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD NormalizedMerchant nvarchar(256) NULL;
IF COL_LENGTH('dbo.cfTransactionRules', 'Source') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD [Source] nvarchar(32) NOT NULL CONSTRAINT DF_cfTransactionRules_Source DEFAULT 'manual';
IF COL_LENGTH('dbo.cfTransactionRules', 'ApprovalState') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD ApprovalState nvarchar(32) NOT NULL CONSTRAINT DF_cfTransactionRules_ApprovalState DEFAULT 'approved';
IF COL_LENGTH('dbo.cfTransactionRules', 'MatchCount') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD MatchCount int NOT NULL CONSTRAINT DF_cfTransactionRules_MatchCount DEFAULT 0;
IF COL_LENGTH('dbo.cfTransactionRules', 'Confidence') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD Confidence decimal(5,4) NULL;
IF COL_LENGTH('dbo.cfTransactionRules', 'EvidenceSummary') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD EvidenceSummary nvarchar(1000) NULL;
IF COL_LENGTH('dbo.cfTransactionRules', 'LastUsedAtUtc') IS NULL
    ALTER TABLE dbo.cfTransactionRules ADD LastUsedAtUtc datetime2 NULL;
""");
        migrationBuilder.Sql("""
IF OBJECT_ID('dbo.cfTransactionRules', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfTransactionRules_UserId_AccountId_MerchantEntityId' AND object_id = OBJECT_ID('dbo.cfTransactionRules'))
    CREATE INDEX IX_cfTransactionRules_UserId_AccountId_MerchantEntityId ON dbo.cfTransactionRules(UserId, AccountId, MerchantEntityId);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Legacy-compatible migration: columns are intentionally retained to avoid destructive downgrades.
    }
}
