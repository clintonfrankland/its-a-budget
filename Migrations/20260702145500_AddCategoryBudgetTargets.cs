using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryBudgetTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cfCategoryBudgetTargets (
        CategoryBudgetTargetId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_cfCategoryBudgetTargets PRIMARY KEY,
        UserId INT NOT NULL,
        CategoryId INT NOT NULL,
        BudgetMonth DATE NOT NULL,
        PlannedAmount DECIMAL(18, 2) NOT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfCategoryBudgetTargets_UpdatedAtUtc DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT CK_cfCategoryBudgetTargets_PlannedAmount_NonNegative CHECK (PlannedAmount >= 0),
        CONSTRAINT FK_cfCategoryBudgetTargets_cfUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.cfUsers(UserId),
        CONSTRAINT FK_cfCategoryBudgetTargets_cfCategories_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.cfCategories(CategoryId)
    );
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_cfCategoryBudgetTargets_User_Category_Month'
      AND object_id = OBJECT_ID('dbo.cfCategoryBudgetTargets')
)
BEGIN
    CREATE UNIQUE INDEX UX_cfCategoryBudgetTargets_User_Category_Month
        ON dbo.cfCategoryBudgetTargets(UserId, CategoryId, BudgetMonth);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_cfCategoryBudgetTargets_CategoryId'
      AND object_id = OBJECT_ID('dbo.cfCategoryBudgetTargets')
)
BEGIN
    CREATE INDEX IX_cfCategoryBudgetTargets_CategoryId
        ON dbo.cfCategoryBudgetTargets(CategoryId);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL
    DROP TABLE dbo.cfCategoryBudgetTargets;
");
        }
    }
}
