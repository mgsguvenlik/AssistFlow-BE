using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDbConf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "IsComplated",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "IsSended",
                table: "ServicesRequests");

            migrationBuilder.RenameColumn(
                name: "ReconciliationStatus",
                table: "WorkFlows",
                newName: "WorkFlowStatus");

            migrationBuilder.AddColumn<bool>(
                name: "IsAgreement",
                table: "WorkFlows",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseStatus",
                table: "Warehouses",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAgreement",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "WarehouseStatus",
                table: "Warehouses");

            migrationBuilder.RenameColumn(
                name: "WorkFlowStatus",
                table: "WorkFlows",
                newName: "ReconciliationStatus");

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "WorkFlows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplated",
                table: "WorkFlows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSended",
                table: "ServicesRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
