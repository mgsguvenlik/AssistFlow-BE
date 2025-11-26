using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkFlowReviewLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkFlowId = table.Column<long>(type: "bigint", nullable: false),
                    RequestNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FromStepId = table.Column<long>(type: "bigint", nullable: true),
                    FromStepCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ToStepId = table.Column<long>(type: "bigint", nullable: true),
                    ToStepCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowReviewLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowReviewLogs_RequestNo",
                table: "WorkFlowReviewLogs",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowReviewLogs_WorkFlowId_CreatedDate",
                table: "WorkFlowReviewLogs",
                columns: new[] { "WorkFlowId", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkFlowReviewLogs");
        }
    }
}
