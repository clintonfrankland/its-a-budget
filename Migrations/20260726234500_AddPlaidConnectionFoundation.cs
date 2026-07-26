using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations;

/// <summary>Creates the isolated Plaid Item and explicit account-mapping tables for the legacy Budget schema.</summary>
public partial class AddPlaidConnectionFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID('cfPlaidItems', 'U') IS NULL
BEGIN
    CREATE TABLE cfPlaidItems (
        PlaidItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        ItemId NVARCHAR(128) NOT NULL,
        EncryptedAccessToken NVARCHAR(MAX) NOT NULL,
        InstitutionId NVARCHAR(128) NULL,
        InstitutionName NVARCHAR(256) NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL,
        DisconnectedAtUtc DATETIME2 NULL,
        CONSTRAINT FK_cfPlaidItems_cfUsers_UserId FOREIGN KEY (UserId) REFERENCES cfUsers(UserId)
    );
    CREATE UNIQUE INDEX UX_cfPlaidItems_ItemId ON cfPlaidItems(ItemId);
    CREATE INDEX IX_cfPlaidItems_UserId ON cfPlaidItems(UserId);
END

IF OBJECT_ID('cfPlaidAccountMappings', 'U') IS NULL
BEGIN
    CREATE TABLE cfPlaidAccountMappings (
        PlaidAccountMappingId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PlaidItemId INT NOT NULL,
        PlaidAccountId NVARCHAR(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
        BudgetAccountId INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL,
        CONSTRAINT FK_cfPlaidAccountMappings_cfPlaidItems_PlaidItemId FOREIGN KEY (PlaidItemId) REFERENCES cfPlaidItems(PlaidItemId) ON DELETE CASCADE,
        CONSTRAINT FK_cfPlaidAccountMappings_cfAccounts_BudgetAccountId FOREIGN KEY (BudgetAccountId) REFERENCES cfAccounts(AccountId)
    );
    CREATE UNIQUE INDEX UX_cfPlaidAccountMappings_Item_Account ON cfPlaidAccountMappings(PlaidItemId, PlaidAccountId);
    CREATE UNIQUE INDEX UX_cfPlaidAccountMappings_BudgetAccount ON cfPlaidAccountMappings(BudgetAccountId);
END");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID('cfPlaidAccountMappings', 'U') IS NOT NULL DROP TABLE cfPlaidAccountMappings;
IF OBJECT_ID('cfPlaidItems', 'U') IS NOT NULL DROP TABLE cfPlaidItems;");
    }
}
