using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkFlowActivityRecordTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkFlowActivityRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkFlowId = table.Column<long>(type: "bigint", nullable: true),
                    RequestNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActionType = table.Column<short>(type: "smallint", nullable: false),
                    FromStepCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ToStepCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PerformedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowActivityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkFlowActivityRecords_WorkFlows_WorkFlowId",
                        column: x => x.WorkFlowId,
                        principalTable: "WorkFlows",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowActivityRecords_OccurredAtUtc",
                table: "WorkFlowActivityRecords",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowActivityRecords_RequestNo",
                table: "WorkFlowActivityRecords",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowActivityRecords_WorkFlowId",
                table: "WorkFlowActivityRecords",
                column: "WorkFlowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkFlowActivityRecords");
        }
    }
}
