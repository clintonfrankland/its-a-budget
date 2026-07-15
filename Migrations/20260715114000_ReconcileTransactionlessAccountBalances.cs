using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClintonFrankland.Migrations;

[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260715114000_ReconcileTransactionlessAccountBalances")]
public partial class ReconcileTransactionlessAccountBalances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
UPDATE accountRow
SET BeginningBalance = accountRow.Balance,
    ClearedBalance = accountRow.Balance
FROM dbo.cfAccounts AS accountRow
WHERE accountRow.BeginningBalance = 0
  AND accountRow.Balance <> 0
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.cfTransactions AS transactionRow
      WHERE transactionRow.AccountId = accountRow.AccountId
  );
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous zero opening balance cannot be distinguished from later user edits.
    }
}
