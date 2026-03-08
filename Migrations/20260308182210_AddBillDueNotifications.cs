using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddBillDueNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded SQL for legacy databases.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfBillDueNotificationSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cfBillDueNotificationSettings (
        Id INT NOT NULL CONSTRAINT PK_cfBillDueNotificationSettings PRIMARY KEY,
        IsEnabled BIT NOT NULL CONSTRAINT DF_cfBillDueNotificationSettings_IsEnabled DEFAULT(0),
        DueSoonDays INT NOT NULL CONSTRAINT DF_cfBillDueNotificationSettings_DueSoonDays DEFAULT(3),
        PastDueEnabled BIT NOT NULL CONSTRAINT DF_cfBillDueNotificationSettings_PastDueEnabled DEFAULT(1),
        PastDueMaxDays INT NOT NULL CONSTRAINT DF_cfBillDueNotificationSettings_PastDueMaxDays DEFAULT(30),
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfBillDueNotificationSettings_UpdatedAtUtc DEFAULT(SYSUTCDATETIME())
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.cfBillDueNotificationSettings WHERE Id = 1)
BEGIN
    INSERT INTO dbo.cfBillDueNotificationSettings (Id, IsEnabled, DueSoonDays, PastDueEnabled, PastDueMaxDays, UpdatedAtUtc)
    VALUES (1, 0, 3, 1, 30, SYSUTCDATETIME());
END

IF OBJECT_ID('dbo.cfNotificationSendLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cfNotificationSendLog (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_cfNotificationSendLog PRIMARY KEY,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfNotificationSendLog_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
        UserId INT NOT NULL,
        BudgetId INT NULL,
        NoticeLocalDate DATETIME2 NOT NULL,
        NoticeType NVARCHAR(32) NOT NULL,
        Recipient NVARCHAR(256) NOT NULL,
        Subject NVARCHAR(256) NOT NULL,
        Status NVARCHAR(16) NOT NULL,
        ErrorMessage NVARCHAR(1024) NULL
    );

    CREATE INDEX IX_cfNotificationSendLog_CreatedAtUtc ON dbo.cfNotificationSendLog(CreatedAtUtc);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_cfNotificationSendLog_User_Budget_Type_Date'
      AND object_id = OBJECT_ID('dbo.cfNotificationSendLog')
)
BEGIN
    CREATE UNIQUE INDEX UX_cfNotificationSendLog_User_Budget_Type_Date
        ON dbo.cfNotificationSendLog(UserId, BudgetId, NoticeType, NoticeLocalDate)
        WHERE BudgetId IS NOT NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfNotificationSendLog', 'U') IS NOT NULL
    DROP TABLE dbo.cfNotificationSendLog;

IF OBJECT_ID('dbo.cfBillDueNotificationSettings', 'U') IS NOT NULL
    DROP TABLE dbo.cfBillDueNotificationSettings;
");
        }
    }
}
