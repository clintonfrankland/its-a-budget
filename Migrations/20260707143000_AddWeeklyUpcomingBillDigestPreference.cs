using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ClintonFranklandDbContext))]
    [Migration("20260707143000_AddWeeklyUpcomingBillDigestPreference")]
    public partial class AddWeeklyUpcomingBillDigestPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfUsers', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfUsers', 'ReceiveWeeklyUpcomingBillDigest') IS NULL
BEGIN
    ALTER TABLE dbo.cfUsers
        ADD ReceiveWeeklyUpcomingBillDigest BIT NOT NULL
            CONSTRAINT DF_cfUsers_ReceiveWeeklyUpcomingBillDigest DEFAULT(0);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfUsers', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfUsers', 'ReceiveWeeklyUpcomingBillDigest') IS NOT NULL
BEGIN
    DECLARE @constraintName sysname;
    SELECT @constraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.cfUsers')
      AND c.name = 'ReceiveWeeklyUpcomingBillDigest';

    IF @constraintName IS NOT NULL
        EXEC(N'ALTER TABLE dbo.cfUsers DROP CONSTRAINT [' + @constraintName + N']');

    ALTER TABLE dbo.cfUsers DROP COLUMN ReceiveWeeklyUpcomingBillDigest;
END
");
        }
    }
}
