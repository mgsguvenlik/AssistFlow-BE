using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSystemAssignmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCustomerSystems");

            migrationBuilder.CreateTable(
                name: "CustomerSystemAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerSystemId = table.Column<long>(type: "bigint", nullable: false),
                    HasMaintenanceContract = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSystemAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerSystemAssignments_CustomerSystem_CustomerSystemId",
                        column: x => x.CustomerSystemId,
                        principalTable: "CustomerSystem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerSystemAssignments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSystemAssignments_CustomerId",
                table: "CustomerSystemAssignments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSystemAssignments_CustomerSystemId",
                table: "CustomerSystemAssignments",
                column: "CustomerSystemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerSystemAssignments");

            migrationBuilder.CreateTable(
                name: "CustomerCustomerSystems",
                columns: table => new
                {
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerSystemId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCustomerSystems", x => new { x.CustomerId, x.CustomerSystemId });
                    table.ForeignKey(
                        name: "FK_CustomerCustomerSystems_CustomerSystem_CustomerSystemId",
                        column: x => x.CustomerSystemId,
                        principalTable: "CustomerSystem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerCustomerSystems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCustomerSystems_CustomerSystemId",
                table: "CustomerCustomerSystems",
                column: "CustomerSystemId");
        }
    }
}
