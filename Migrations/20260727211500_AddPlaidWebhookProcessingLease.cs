using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ClintonFrankland.Migrations;

[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260727211500_AddPlaidWebhookProcessingLease")]
public partial class AddPlaidWebhookProcessingLease : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
IF COL_LENGTH('cfPlaidWebhookDeliveries', 'ProcessingStartedAtUtc') IS NULL
    ALTER TABLE cfPlaidWebhookDeliveries ADD ProcessingStartedAtUtc DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfPlaidWebhookDeliveries_Status_ProcessingStartedAtUtc' AND object_id = OBJECT_ID('cfPlaidWebhookDeliveries'))
    CREATE INDEX IX_cfPlaidWebhookDeliveries_Status_ProcessingStartedAtUtc ON cfPlaidWebhookDeliveries(Status, ProcessingStartedAtUtc);");

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
