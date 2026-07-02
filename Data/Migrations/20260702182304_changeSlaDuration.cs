using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class changeSlaDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationBeforeDays",
                table: "WorkFlowSlaSettings");

            migrationBuilder.DropColumn(
                name: "SlaDurationDays",
                table: "WorkFlowSlaSettings");

            migrationBuilder.AddColumn<int>(
                name: "NotificationBeforeHours",
                table: "WorkFlowSlaSettings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "Bildirim gönderilecek süre (saat önce)");

            migrationBuilder.AddColumn<int>(
                name: "SlaDurationHours",
                table: "WorkFlowSlaSettings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "SLA süresi (saat)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationBeforeHours",
                table: "WorkFlowSlaSettings");

            migrationBuilder.DropColumn(
                name: "SlaDurationHours",
                table: "WorkFlowSlaSettings");

            migrationBuilder.AddColumn<int>(
                name: "NotificationBeforeDays",
                table: "WorkFlowSlaSettings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "Bildirim gönderilecek süre (gün önce)");

            migrationBuilder.AddColumn<int>(
                name: "SlaDurationDays",
                table: "WorkFlowSlaSettings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "SLA süresi (gün)");
        }
    }
}
