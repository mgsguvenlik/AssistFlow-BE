using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkFlowTransitionAndStepsv2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequests_WorkFlowStepes_WorkFlowStepId",
                table: "ServicesRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlows_WorkFlowStepes_CurrentStepId",
                table: "WorkFlows");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlowStepes_WorkFlows_WorkFlowId",
                table: "WorkFlowStepes");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowStepes_FromStepId",
                table: "WorkFlowTransitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowStepes_ToStepId",
                table: "WorkFlowTransitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkFlowStepes",
                table: "WorkFlowStepes");

            migrationBuilder.DropIndex(
                name: "IX_WorkFlowStepes_WorkFlowId",
                table: "WorkFlowStepes");

            migrationBuilder.DropColumn(
                name: "WorkFlowId",
                table: "WorkFlowStepes");

            migrationBuilder.RenameTable(
                name: "WorkFlowStepes",
                newName: "WorkFlowSteps");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkFlowSteps",
                table: "WorkFlowSteps",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequests_WorkFlowSteps_WorkFlowStepId",
                table: "ServicesRequests",
                column: "WorkFlowStepId",
                principalTable: "WorkFlowSteps",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlows_WorkFlowSteps_CurrentStepId",
                table: "WorkFlows",
                column: "CurrentStepId",
                principalTable: "WorkFlowSteps",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowSteps_FromStepId",
                table: "WorkFlowTransitions",
                column: "FromStepId",
                principalTable: "WorkFlowSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowSteps_ToStepId",
                table: "WorkFlowTransitions",
                column: "ToStepId",
                principalTable: "WorkFlowSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequests_WorkFlowSteps_WorkFlowStepId",
                table: "ServicesRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlows_WorkFlowSteps_CurrentStepId",
                table: "WorkFlows");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowSteps_FromStepId",
                table: "WorkFlowTransitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowSteps_ToStepId",
                table: "WorkFlowTransitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkFlowSteps",
                table: "WorkFlowSteps");

            migrationBuilder.RenameTable(
                name: "WorkFlowSteps",
                newName: "WorkFlowStepes");

            migrationBuilder.AddColumn<long>(
                name: "WorkFlowId",
                table: "WorkFlowStepes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkFlowStepes",
                table: "WorkFlowStepes",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowStepes_WorkFlowId",
                table: "WorkFlowStepes",
                column: "WorkFlowId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlowStepes_WorkFlows_WorkFlowId",
                table: "WorkFlowStepes",
                column: "WorkFlowId",
                principalTable: "WorkFlows",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowStepes_FromStepId",
                table: "WorkFlowTransitions",
                column: "FromStepId",
                principalTable: "WorkFlowStepes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlowTransitions_WorkFlowStepes_ToStepId",
                table: "WorkFlowTransitions",
                column: "ToStepId",
                principalTable: "WorkFlowStepes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
