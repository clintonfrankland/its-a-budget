using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ClintonFranklandDbContext))]
    [Migration("20260705201000_AddBudgetInviteUserName")]
    public partial class AddBudgetInviteUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfBudgetInvites', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfBudgetInvites', 'InviteeUserName') IS NULL
    ALTER TABLE dbo.cfBudgetInvites ADD InviteeUserName NVARCHAR(64) NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfBudgetInvites', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfBudgetInvites', 'InviteeUserName') IS NOT NULL
    ALTER TABLE dbo.cfBudgetInvites DROP COLUMN InviteeUserName;
");
        }
    }
}
