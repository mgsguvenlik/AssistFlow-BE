using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkFlowTransitionAndSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequests_WorkFlowStatuses_WorkFlowStatusId",
                table: "ServicesRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlows_WorkFlowStatuses_StatuId",
                table: "WorkFlows");

            migrationBuilder.DropTable(
                name: "WorkFlowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_WorkFlows_StatuId",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "StatuId",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "SendedStatusId",
                table: "ServicesRequests");

            migrationBuilder.RenameColumn(
                name: "WorkFlowStatusId",
                table: "ServicesRequests",
                newName: "WorkFlowStepId");

            migrationBuilder.RenameIndex(
                name: "IX_ServicesRequests_WorkFlowStatusId",
                table: "ServicesRequests",
                newName: "IX_ServicesRequests_WorkFlowStepId");

            migrationBuilder.AddColumn<long>(
                name: "CurrentStepId",
                table: "WorkFlows",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServicesRequestStatus",
                table: "ServicesRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WorkFlowStepes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    WorkFlowId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowStepes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkFlowStepes_WorkFlows_WorkFlowId",
                        column: x => x.WorkFlowId,
                        principalTable: "WorkFlows",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkFlowTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromStepId = table.Column<long>(type: "bigint", nullable: false),
                    ToStepId = table.Column<long>(type: "bigint", nullable: false),
                    TransitionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkFlowTransitions_WorkFlowStepes_FromStepId",
                        column: x => x.FromStepId,
                        principalTable: "WorkFlowStepes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkFlowTransitions_WorkFlowStepes_ToStepId",
                        column: x => x.ToStepId,
                        principalTable: "WorkFlowStepes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlows_CurrentStepId",
                table: "WorkFlows",
                column: "CurrentStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowStepes_WorkFlowId",
                table: "WorkFlowStepes",
                column: "WorkFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowTransitions_FromStepId",
                table: "WorkFlowTransitions",
                column: "FromStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowTransitions_ToStepId",
                table: "WorkFlowTransitions",
                column: "ToStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequests_WorkFlowStepes_WorkFlowStepId",
                table: "ServicesRequests",
                column: "WorkFlowStepId",
                principalTable: "WorkFlowStepes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlows_WorkFlowStepes_CurrentStepId",
                table: "WorkFlows",
                column: "CurrentStepId",
                principalTable: "WorkFlowStepes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequests_WorkFlowStepes_WorkFlowStepId",
                table: "ServicesRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlows_WorkFlowStepes_CurrentStepId",
                table: "WorkFlows");

            migrationBuilder.DropTable(
                name: "WorkFlowTransitions");

            migrationBuilder.DropTable(
                name: "WorkFlowStepes");

            migrationBuilder.DropIndex(
                name: "IX_WorkFlows_CurrentStepId",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "CurrentStepId",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "ServicesRequestStatus",
                table: "ServicesRequests");

            migrationBuilder.RenameColumn(
                name: "WorkFlowStepId",
                table: "ServicesRequests",
                newName: "WorkFlowStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_ServicesRequests_WorkFlowStepId",
                table: "ServicesRequests",
                newName: "IX_ServicesRequests_WorkFlowStatusId");

            migrationBuilder.AddColumn<long>(
                name: "StatuId",
                table: "WorkFlows",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SendedStatusId",
                table: "ServicesRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkFlowStatuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlows_StatuId",
                table: "WorkFlows",
                column: "StatuId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequests_WorkFlowStatuses_WorkFlowStatusId",
                table: "ServicesRequests",
                column: "WorkFlowStatusId",
                principalTable: "WorkFlowStatuses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlows_WorkFlowStatuses_StatuId",
                table: "WorkFlows",
                column: "StatuId",
                principalTable: "WorkFlowStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
