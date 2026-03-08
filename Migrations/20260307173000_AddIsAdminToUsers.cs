using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations;

[DbContext(typeof(ClintonFranklandDbContext))]
[Migration("20260307173000_AddIsAdminToUsers")]
public partial class AddIsAdminToUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: supports existing DBs that may or may not already have the column.
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfUsers','IsAdmin') IS NULL
BEGIN
    ALTER TABLE dbo.cfUsers
    ADD IsAdmin bit NOT NULL
        CONSTRAINT DF_cfUsers_IsAdmin DEFAULT(0);
END
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Best-effort rollback.
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.cfUsers','IsAdmin') IS NOT NULL
BEGIN
    ALTER TABLE dbo.cfUsers DROP CONSTRAINT DF_cfUsers_IsAdmin;
    ALTER TABLE dbo.cfUsers DROP COLUMN IsAdmin;
END
");
    }
}
