using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ykbWorkflowRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YkbWorkFlowActivityRecord_WorkFlows_WorkFlowId",
                schema: "ykb",
                table: "YkbWorkFlowActivityRecord");

            migrationBuilder.AddForeignKey(
                name: "FK_YkbWorkFlowActivityRecord_YkbWorkFlow_WorkFlowId",
                schema: "ykb",
                table: "YkbWorkFlowActivityRecord",
                column: "WorkFlowId",
                principalSchema: "ykb",
                principalTable: "YkbWorkFlow",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YkbWorkFlowActivityRecord_YkbWorkFlow_WorkFlowId",
                schema: "ykb",
                table: "YkbWorkFlowActivityRecord");

            migrationBuilder.AddForeignKey(
                name: "FK_YkbWorkFlowActivityRecord_WorkFlows_WorkFlowId",
                schema: "ykb",
                table: "YkbWorkFlowActivityRecord",
                column: "WorkFlowId",
                principalTable: "WorkFlows",
                principalColumn: "Id");
        }
    }
}
