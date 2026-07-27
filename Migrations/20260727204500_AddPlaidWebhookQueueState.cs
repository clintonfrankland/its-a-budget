using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ClintonFrankland.Migrations;

[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260727204500_AddPlaidWebhookQueueState")]
public partial class AddPlaidWebhookQueueState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
IF COL_LENGTH('cfPlaidWebhookDeliveries', 'Status') IS NULL ALTER TABLE cfPlaidWebhookDeliveries ADD Status NVARCHAR(32) NOT NULL CONSTRAINT DF_cfPlaidWebhookDeliveries_Status DEFAULT('queued');
IF COL_LENGTH('cfPlaidWebhookDeliveries', 'AttemptCount') IS NULL ALTER TABLE cfPlaidWebhookDeliveries ADD AttemptCount INT NOT NULL CONSTRAINT DF_cfPlaidWebhookDeliveries_AttemptCount DEFAULT(0);
IF COL_LENGTH('cfPlaidWebhookDeliveries', 'CompletedAtUtc') IS NULL ALTER TABLE cfPlaidWebhookDeliveries ADD CompletedAtUtc DATETIME2 NULL;
IF COL_LENGTH('cfPlaidWebhookDeliveries', 'ErrorCode') IS NULL ALTER TABLE cfPlaidWebhookDeliveries ADD ErrorCode NVARCHAR(512) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfPlaidWebhookDeliveries_Status' AND object_id = OBJECT_ID('cfPlaidWebhookDeliveries')) CREATE INDEX IX_cfPlaidWebhookDeliveries_Status ON cfPlaidWebhookDeliveries(Status);");
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
