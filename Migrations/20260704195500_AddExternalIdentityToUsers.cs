using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ClintonFranklandDbContext))]
    [Migration("20260704195500_AddExternalIdentityToUsers")]
    public partial class AddExternalIdentityToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH('dbo.cfUsers', 'ExternalProvider') IS NULL ALTER TABLE dbo.cfUsers ADD ExternalProvider NVARCHAR(64) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.cfUsers', 'ExternalSubject') IS NULL ALTER TABLE dbo.cfUsers ADD ExternalSubject NVARCHAR(256) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.cfUsers', 'ExternalEmail') IS NULL ALTER TABLE dbo.cfUsers ADD ExternalEmail NVARCHAR(256) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.cfUsers', 'ExternalDisplayName') IS NULL ALTER TABLE dbo.cfUsers ADD ExternalDisplayName NVARCHAR(128) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.cfUsers', 'LastExternalLoginUtc') IS NULL ALTER TABLE dbo.cfUsers ADD LastExternalLoginUtc DATETIME2 NULL;");
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_cfUsers_ExternalProvider_ExternalSubject_Active'
      AND object_id = OBJECT_ID('dbo.cfUsers')
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UX_cfUsers_ExternalProvider_ExternalSubject_Active
        ON dbo.cfUsers(ExternalProvider, ExternalSubject)
        WHERE IsDeleted = 0
          AND ExternalProvider IS NOT NULL
          AND ExternalSubject IS NOT NULL;');
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_cfUsers_ExternalProvider_ExternalSubject_Active'
      AND object_id = OBJECT_ID('dbo.cfUsers')
)
    DROP INDEX UX_cfUsers_ExternalProvider_ExternalSubject_Active ON dbo.cfUsers;

IF COL_LENGTH('dbo.cfUsers', 'LastExternalLoginUtc') IS NOT NULL
    ALTER TABLE dbo.cfUsers DROP COLUMN LastExternalLoginUtc;

IF COL_LENGTH('dbo.cfUsers', 'ExternalDisplayName') IS NOT NULL
    ALTER TABLE dbo.cfUsers DROP COLUMN ExternalDisplayName;

IF COL_LENGTH('dbo.cfUsers', 'ExternalEmail') IS NOT NULL
    ALTER TABLE dbo.cfUsers DROP COLUMN ExternalEmail;

IF COL_LENGTH('dbo.cfUsers', 'ExternalSubject') IS NOT NULL
    ALTER TABLE dbo.cfUsers DROP COLUMN ExternalSubject;

IF COL_LENGTH('dbo.cfUsers', 'ExternalProvider') IS NOT NULL
    ALTER TABLE dbo.cfUsers DROP COLUMN ExternalProvider;
");
        }
    }
}
