using Microsoft.EntityFrameworkCore.Migrations;

namespace ClintonFrankland.Migrations;

public partial class AddTransactionRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfTransactionRules', 'U') IS NULL
BEGIN
 CREATE TABLE dbo.cfTransactionRules (
  TransactionRuleId INT IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId INT NOT NULL, ContainsText NVARCHAR(256) NOT NULL,
  AccountId INT NULL, MinimumAmount DECIMAL(9,2) NULL, MaximumAmount DECIMAL(9,2) NULL,
  CategoryName NVARCHAR(128) NULL, PayeeName NVARCHAR(255) NULL, Notes NVARCHAR(500) NULL,
  Priority INT NOT NULL, IsEnabled BIT NOT NULL CONSTRAINT DF_cfTransactionRules_IsEnabled DEFAULT(1), UpdatedAtUtc DATETIME2 NOT NULL);
 CREATE INDEX IX_cfTransactionRules_UserId_Priority ON dbo.cfTransactionRules(UserId, Priority);
END");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"IF OBJECT_ID('dbo.cfTransactionRules', 'U') IS NOT NULL DROP TABLE dbo.cfTransactionRules;");
}
