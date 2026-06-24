using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderTypeToServicesRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServicesRequestWorkOrderTypes",
                columns: table => new
                {
                    ServicesRequestId = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicesRequestWorkOrderTypes", x => new { x.ServicesRequestId, x.WorkOrderTypeId });
                    table.ForeignKey(
                        name: "FK_ServicesRequestWorkOrderTypes_ServicesRequests_ServicesRequestId",
                        column: x => x.ServicesRequestId,
                        principalTable: "ServicesRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServicesRequestWorkOrderTypes_WorkOrderTypes_WorkOrderTypeId",
                        column: x => x.WorkOrderTypeId,
                        principalTable: "WorkOrderTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequestWorkOrderTypes_WorkOrderTypeId",
                table: "ServicesRequestWorkOrderTypes",
                column: "WorkOrderTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServicesRequestWorkOrderTypes");
        }
    }
}
