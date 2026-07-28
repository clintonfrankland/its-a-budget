using Microsoft.EntityFrameworkCore.Migrations;

namespace ClintonFrankland.Migrations;

/// <summary>Adds explainable, user-approved learning metadata to the existing cfTransactionRules engine.</summary>
public partial class AddLearnedPlaidTransactionRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.cfPlaidTransactionStaging', 'MerchantEntityId') IS NULL
    ALTER TABLE dbo.cfPlaidTransactionStaging ADD MerchantEntityId nvarchar(128) NULL;
IF COL_LENGTH('dbo.cfTransactionRules', 'MerchantEntityId') IS NULL
BEGIN
    ALTER TABLE dbo.cfTransactionRules ADD MerchantEntityId nvarchar(128) NULL, NormalizedMerchant nvarchar(256) NULL,
        [Source] nvarchar(32) NOT NULL CONSTRAINT DF_cfTransactionRules_Source DEFAULT 'manual',
        ApprovalState nvarchar(32) NOT NULL CONSTRAINT DF_cfTransactionRules_ApprovalState DEFAULT 'approved',
        MatchCount int NOT NULL CONSTRAINT DF_cfTransactionRules_MatchCount DEFAULT 0,
        Confidence decimal(5,4) NULL, EvidenceSummary nvarchar(1000) NULL, LastUsedAtUtc datetime2 NULL;
    CREATE INDEX IX_cfTransactionRules_UserId_AccountId_MerchantEntityId ON dbo.cfTransactionRules(UserId, AccountId, MerchantEntityId);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Legacy-compatible migration: columns are intentionally retained to avoid destructive downgrades.
    }
}
