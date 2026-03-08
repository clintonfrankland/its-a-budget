using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "NotificationDeliveryTime",
                table: "cfUsers",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(8, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "NotificationTimezone",
                table: "cfUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "America/New_York");

            migrationBuilder.AddColumn<bool>(
                name: "ReceiveBillDueNotices",
                table: "cfUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationDeliveryTime",
                table: "cfUsers");

            migrationBuilder.DropColumn(
                name: "NotificationTimezone",
                table: "cfUsers");

            migrationBuilder.DropColumn(
                name: "ReceiveBillDueNotices",
                table: "cfUsers");
        }
    }
}
