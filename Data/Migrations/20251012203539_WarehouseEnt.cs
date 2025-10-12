using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class WarehouseEnt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WarehouseId",
                table: "ServicesRequestProducts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "ProgressApprovers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeliveryDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApproverTechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSended = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warehouses_Users_ApproverTechnicianId",
                        column: x => x.ApproverTechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequestProducts_WarehouseId",
                table: "ServicesRequestProducts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ApproverTechnicianId",
                table: "Warehouses",
                column: "ApproverTechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_Warehouses_WarehouseId",
                table: "ServicesRequestProducts",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_Warehouses_WarehouseId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_ServicesRequestProducts_WarehouseId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "ProgressApprovers");
        }
    }
}
