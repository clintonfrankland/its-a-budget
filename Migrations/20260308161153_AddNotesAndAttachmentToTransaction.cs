using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesAndAttachmentToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "cfTransactions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "cfTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentPath",
                table: "cfTransactions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "cfTransactions");
        }
    }
}
