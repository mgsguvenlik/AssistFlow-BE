using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYkbWorkOrderTypeToServicesRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YkbServicesRequestWorkOrderTypes",
                schema: "ykb",
                columns: table => new
                {
                    YkbServicesRequestId = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YkbServicesRequestWorkOrderTypes", x => new { x.YkbServicesRequestId, x.WorkOrderTypeId });
                    table.ForeignKey(
                        name: "FK_YkbServicesRequestWorkOrderTypes_WorkOrderTypes_WorkOrderTypeId",
                        column: x => x.WorkOrderTypeId,
                        principalTable: "WorkOrderTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YkbServicesRequestWorkOrderTypes_YkbServicesRequest_YkbServicesRequestId",
                        column: x => x.YkbServicesRequestId,
                        principalSchema: "ykb",
                        principalTable: "YkbServicesRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequestWorkOrderTypes_WorkOrderTypeId",
                schema: "ykb",
                table: "YkbServicesRequestWorkOrderTypes",
                column: "WorkOrderTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YkbServicesRequestWorkOrderTypes",
                schema: "ykb");
        }
    }
}
