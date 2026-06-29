using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class addQnbWorkOrderTypeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QnbServicesRequestWorkOrderTypes",
                columns: table => new
                {
                    QnbServicesRequestId = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbServicesRequestWorkOrderTypes", x => new { x.QnbServicesRequestId, x.WorkOrderTypeId });
                    table.ForeignKey(
                        name: "FK_QnbServicesRequestWorkOrderTypes_QnbServicesRequest_QnbServicesRequestId",
                        column: x => x.QnbServicesRequestId,
                        principalSchema: "qnb",
                        principalTable: "QnbServicesRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QnbServicesRequestWorkOrderTypes_WorkOrderTypes_WorkOrderTypeId",
                        column: x => x.WorkOrderTypeId,
                        principalTable: "WorkOrderTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequestWorkOrderTypes_WorkOrderTypeId",
                table: "QnbServicesRequestWorkOrderTypes",
                column: "WorkOrderTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QnbServicesRequestWorkOrderTypes");
        }
    }
}
