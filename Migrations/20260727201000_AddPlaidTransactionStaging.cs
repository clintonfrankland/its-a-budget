using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace ClintonFrankland.Migrations;
[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260727201000_AddPlaidTransactionStaging")]
public partial class AddPlaidTransactionStaging : Migration
{
 protected override void Up(MigrationBuilder m) => m.Sql(@"
IF COL_LENGTH('cfPlaidItems','TransactionsCursor') IS NULL ALTER TABLE cfPlaidItems ADD TransactionsCursor NVARCHAR(512) NULL;
IF OBJECT_ID('cfPlaidTransactionStaging','U') IS NULL CREATE TABLE cfPlaidTransactionStaging (PlaidTransactionStagingId INT IDENTITY PRIMARY KEY, UserId INT NOT NULL, PlaidItemId INT NOT NULL, BudgetAccountId INT NOT NULL, PlaidTransactionId NVARCHAR(128) COLLATE Latin1_General_100_BIN2 NOT NULL, PendingTransactionId NVARCHAR(128) NULL, PlaidAccountId NVARCHAR(128) NOT NULL, PlaidAmount DECIMAL(19,4) NOT NULL, CurrencyCode NVARCHAR(3) NOT NULL, TransactionDate DATE NOT NULL, IsPending BIT NOT NULL, IsRemoved BIT NOT NULL, MerchantName NVARCHAR(256) NULL, Name NVARCHAR(256) NULL, FirstSeenAtUtc DATETIME2 NOT NULL, LastSeenAtUtc DATETIME2 NOT NULL, CONSTRAINT UX_cfPlaidTransactionStaging UNIQUE(PlaidItemId,PlaidTransactionId));
IF OBJECT_ID('cfPlaidSyncRuns','U') IS NULL CREATE TABLE cfPlaidSyncRuns (PlaidSyncRunId INT IDENTITY PRIMARY KEY, PlaidItemId INT NOT NULL, Status NVARCHAR(64) NOT NULL, CursorBefore NVARCHAR(MAX) NULL, CursorAfter NVARCHAR(MAX) NULL, AddedCount INT NOT NULL, ModifiedCount INT NOT NULL, RemovedCount INT NOT NULL, ErrorCode NVARCHAR(512) NULL, StartedAtUtc DATETIME2 NOT NULL, CompletedAtUtc DATETIME2 NULL);
IF OBJECT_ID('cfPlaidWebhookDeliveries','U') IS NULL CREATE TABLE cfPlaidWebhookDeliveries (PlaidWebhookDeliveryId INT IDENTITY PRIMARY KEY, DeliveryKey NVARCHAR(128) NOT NULL UNIQUE, ItemId NVARCHAR(128) NULL, WebhookType NVARCHAR(64) NULL, ReceivedAtUtc DATETIME2 NOT NULL, QueuedAtUtc DATETIME2 NULL);");
 protected override void Down(MigrationBuilder m) => m.Sql("IF OBJECT_ID('cfPlaidWebhookDeliveries','U') IS NOT NULL DROP TABLE cfPlaidWebhookDeliveries; IF OBJECT_ID('cfPlaidSyncRuns','U') IS NOT NULL DROP TABLE cfPlaidSyncRuns; IF OBJECT_ID('cfPlaidTransactionStaging','U') IS NOT NULL DROP TABLE cfPlaidTransactionStaging;");
}
