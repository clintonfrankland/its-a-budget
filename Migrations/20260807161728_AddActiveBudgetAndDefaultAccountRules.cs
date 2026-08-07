using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveBudgetAndDefaultAccountRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('dbo.cfUsers', 'ActiveSharedBudgetId') IS NULL
BEGIN
    ALTER TABLE dbo.cfUsers ADD ActiveSharedBudgetId int NULL;
END
""");

            migrationBuilder.Sql("""
UPDATE users
SET ActiveSharedBudgetId = memberships.SharedBudgetId
FROM dbo.cfUsers AS users
CROSS APPLY
(
    SELECT TOP (1) member.SharedBudgetId
    FROM dbo.cfBudgetMembers AS member
    WHERE member.UserId = users.UserId
      AND member.Status = 'Active'
    ORDER BY member.SharedBudgetId
) AS memberships
WHERE users.ActiveSharedBudgetId IS NULL;
""");

            migrationBuilder.Sql("""
;WITH RankedAccounts AS
(
    SELECT AccountId,
           ROW_NUMBER() OVER
           (
               PARTITION BY SharedBudgetId
               ORDER BY CASE WHEN IsDefault = 1 THEN 0 ELSE 1 END, AccountId
           ) AS DefaultRank
    FROM dbo.cfAccounts
    WHERE SharedBudgetId IS NOT NULL
      AND ISNULL(IsDeleted, 0) = 0
)
UPDATE accountRow
SET IsDefault = CASE WHEN ranked.DefaultRank = 1 THEN 1 ELSE 0 END
FROM dbo.cfAccounts AS accountRow
INNER JOIN RankedAccounts AS ranked ON ranked.AccountId = accountRow.AccountId;
""");

            migrationBuilder.Sql("""
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_cfAccounts_SharedBudget_Default'
      AND object_id = OBJECT_ID('dbo.cfAccounts')
)
BEGIN
    CREATE UNIQUE INDEX UX_cfAccounts_SharedBudget_Default
        ON dbo.cfAccounts (SharedBudgetId)
        WHERE IsDefault = 1 AND SharedBudgetId IS NOT NULL;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_cfAccounts_SharedBudget_Default'
      AND object_id = OBJECT_ID('dbo.cfAccounts')
)
BEGIN
    DROP INDEX UX_cfAccounts_SharedBudget_Default ON dbo.cfAccounts;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('dbo.cfUsers', 'ActiveSharedBudgetId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.cfUsers DROP COLUMN ActiveSharedBudgetId;
END
""");
        }
    }
}
