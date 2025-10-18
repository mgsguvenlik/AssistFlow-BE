using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalTech : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Users_ApproverTechnicianId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_ApproverTechnicianId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ApproverTechnicianId",
                table: "Warehouses");

            migrationBuilder.AddColumn<long>(
                name: "ApproverTechnicianId",
                table: "WorkFlows",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlows_ApproverTechnicianId",
                table: "WorkFlows",
                column: "ApproverTechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlows_Users_ApproverTechnicianId",
                table: "WorkFlows",
                column: "ApproverTechnicianId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlows_Users_ApproverTechnicianId",
                table: "WorkFlows");

            migrationBuilder.DropIndex(
                name: "IX_WorkFlows_ApproverTechnicianId",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "ApproverTechnicianId",
                table: "WorkFlows");

            migrationBuilder.AddColumn<long>(
                name: "ApproverTechnicianId",
                table: "Warehouses",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ApproverTechnicianId",
                table: "Warehouses",
                column: "ApproverTechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Users_ApproverTechnicianId",
                table: "Warehouses",
                column: "ApproverTechnicianId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
