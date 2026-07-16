using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddSpendingAllowanceBudgetItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfBudgets', 'IsSpendingAllowance') IS NULL
BEGIN
    ALTER TABLE dbo.cfBudgets
        ADD IsSpendingAllowance BIT NOT NULL
            CONSTRAINT DF_cfBudgets_IsSpendingAllowance DEFAULT(0);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL
BEGIN
    UPDATE budgetRow
    SET IsSpendingAllowance = 1,
        IsAutomatic = 0,
        IsLate = 0,
        IsBill = 0,
        PayeeId = NULL
    FROM dbo.cfBudgets AS budgetRow
    INNER JOIN dbo.cfCategories AS categoryRow ON categoryRow.CategoryId = budgetRow.CategoryId
    WHERE budgetRow.BudgetTypeID = 1
      AND COALESCE(budgetRow.IsBill, 0) = 0
      AND COALESCE(budgetRow.IsAutomatic, 0) = 0
      AND budgetRow.PayeeId IS NULL
      AND LOWER(LTRIM(RTRIM(COALESCE(budgetRow.BudgetName, '')))) = LOWER(LTRIM(RTRIM(COALESCE(categoryRow.CategoryName, ''))))
      AND EXISTS (
          SELECT 1
          FROM dbo.cfCategoryBudgetTargets AS matchingTarget
          WHERE matchingTarget.CategoryId = budgetRow.CategoryId
            AND (
                (matchingTarget.SharedBudgetId IS NOT NULL AND matchingTarget.SharedBudgetId = budgetRow.SharedBudgetId)
                OR (matchingTarget.SharedBudgetId IS NULL AND budgetRow.SharedBudgetId IS NULL AND matchingTarget.UserId = budgetRow.UserId)
            )
      )
      AND budgetRow.BudgetId = (
          SELECT MIN(candidate.BudgetId)
          FROM dbo.cfBudgets AS candidate
            WHERE candidate.CategoryId = budgetRow.CategoryId
            AND candidate.BudgetTypeID = 1
            AND COALESCE(candidate.IsBill, 0) = 0
            AND COALESCE(candidate.IsAutomatic, 0) = 0
            AND candidate.PayeeId IS NULL
            AND LOWER(LTRIM(RTRIM(COALESCE(candidate.BudgetName, '')))) = LOWER(LTRIM(RTRIM(COALESCE(categoryRow.CategoryName, ''))))
            AND (
                (budgetRow.SharedBudgetId IS NOT NULL AND candidate.SharedBudgetId = budgetRow.SharedBudgetId)
                OR (budgetRow.SharedBudgetId IS NULL AND candidate.SharedBudgetId IS NULL AND candidate.UserId = budgetRow.UserId)
            )
      );

    ;WITH LatestTarget AS (
        SELECT targetRow.*,
               ROW_NUMBER() OVER (
                   PARTITION BY COALESCE(targetRow.SharedBudgetId, -targetRow.UserId), targetRow.CategoryId
                   ORDER BY targetRow.BudgetMonth DESC, targetRow.CategoryBudgetTargetId DESC
               ) AS TargetRank
        FROM dbo.cfCategoryBudgetTargets AS targetRow
    )
    INSERT INTO dbo.cfBudgets (
        BudgetTypeID,
        BudgetName,
        FrequencyID,
        NextDueDate,
        EndDate,
        Amount,
        CategoryId,
        UserId,
        SharedBudgetId,
        IsAutomatic,
        IsLate,
        IsBill,
        IsSpendingAllowance,
        PayeeId)
    SELECT
        1,
        CONCAT(COALESCE(NULLIF(LTRIM(RTRIM(categoryRow.CategoryName)), ''), 'Category'), ' allowance'),
        4,
        DATEADD(MONTH, 1, CAST(targetRow.BudgetMonth AS DATETIME2)),
        CAST('1970-01-01' AS DATETIME2),
        targetRow.PlannedAmount,
        targetRow.CategoryId,
        targetRow.UserId,
        targetRow.SharedBudgetId,
        0,
        0,
        0,
        1,
        NULL
    FROM LatestTarget AS targetRow
    INNER JOIN dbo.cfCategories AS categoryRow ON categoryRow.CategoryId = targetRow.CategoryId
    WHERE targetRow.TargetRank = 1
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.cfBudgets AS budgetRow
          WHERE budgetRow.IsSpendingAllowance = 1
            AND budgetRow.CategoryId = targetRow.CategoryId
            AND (
                (targetRow.SharedBudgetId IS NOT NULL AND budgetRow.SharedBudgetId = targetRow.SharedBudgetId)
                OR (targetRow.SharedBudgetId IS NULL AND budgetRow.SharedBudgetId IS NULL AND budgetRow.UserId = targetRow.UserId)
            )
      );
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfBudgets', 'IsSpendingAllowance') IS NOT NULL
BEGIN
    DECLARE @constraintName sysname;
    SELECT @constraintName = dc.name
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.cfBudgets')
      AND c.name = 'IsSpendingAllowance';

    IF @constraintName IS NOT NULL
        EXEC('ALTER TABLE dbo.cfBudgets DROP CONSTRAINT [' + @constraintName + ']');

    ALTER TABLE dbo.cfBudgets DROP COLUMN IsSpendingAllowance;
END
");
        }
    }
}
