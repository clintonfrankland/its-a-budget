using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSmtpSettingsForPatternA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The database schema for this app predates EF migrations.
            // This migration upgrades the existing cfSmtpSettings table to support server-wide SMTP settings
            // without storing SMTP credentials (Pattern A).

            // 1) Create table if missing
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfSmtpSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cfSmtpSettings (
        Id INT NOT NULL PRIMARY KEY,
        IsEnabled BIT NOT NULL CONSTRAINT DF_cfSmtpSettings_IsEnabled DEFAULT(0),
        SenderEmail NVARCHAR(256) NULL,
        FromName NVARCHAR(128) NULL,
        TlsMode INT NOT NULL CONSTRAINT DF_cfSmtpSettings_TlsMode DEFAULT(1),
        AllowInvalidCerts BIT NOT NULL CONSTRAINT DF_cfSmtpSettings_AllowInvalidCerts DEFAULT(0),
        TestRecipientEmail NVARCHAR(256) NULL,
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfSmtpSettings_UpdatedAtUtc DEFAULT(SYSUTCDATETIME())
    );
END
");

            // 2) Add/upgrade columns (split into separate statements so SQL Server compiles after DDL)
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfSmtpSettings', 'SenderEmail') IS NULL ALTER TABLE dbo.cfSmtpSettings ADD SenderEmail NVARCHAR(256) NULL;
IF COL_LENGTH('dbo.cfSmtpSettings', 'FromName') IS NULL ALTER TABLE dbo.cfSmtpSettings ADD FromName NVARCHAR(128) NULL;
IF COL_LENGTH('dbo.cfSmtpSettings', 'TestRecipientEmail') IS NULL ALTER TABLE dbo.cfSmtpSettings ADD TestRecipientEmail NVARCHAR(256) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfSmtpSettings', 'TlsMode') IS NULL
BEGIN
    ALTER TABLE dbo.cfSmtpSettings
        ADD TlsMode INT NOT NULL
            CONSTRAINT DF_cfSmtpSettings_TlsMode DEFAULT(1);
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfSmtpSettings', 'AllowInvalidCerts') IS NULL
BEGIN
    ALTER TABLE dbo.cfSmtpSettings
        ADD AllowInvalidCerts BIT NOT NULL
            CONSTRAINT DF_cfSmtpSettings_AllowInvalidCerts DEFAULT(0);
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfSmtpSettings', 'UpdatedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.cfSmtpSettings
        ADD UpdatedAtUtc DATETIME2 NOT NULL
            CONSTRAINT DF_cfSmtpSettings_UpdatedAtUtc DEFAULT(SYSUTCDATETIME());
END
");

            // 3) Seed the single server-wide row (use dynamic SQL to avoid compile-time column validation)
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM dbo.cfSmtpSettings WHERE Id = 1)
BEGIN
    EXEC(N'INSERT INTO dbo.cfSmtpSettings (Id, IsEnabled, SenderEmail, FromName, TlsMode, AllowInvalidCerts, TestRecipientEmail, UpdatedAtUtc)
          VALUES (1, 0, NULL, NULL, 1, 0, NULL, SYSUTCDATETIME());');
END
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op. We don't want to drop user data.
        }
    }
}
