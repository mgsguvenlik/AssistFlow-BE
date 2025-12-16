using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class archiveTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "WorkFlowArchives",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchiveReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ServicesRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServicesRequestProductsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproverTechnicianJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerApproverJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkFlowJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkFlowReviewLogsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalServiceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalServiceImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalServiceFormImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarehouseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PricingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalApprovalJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowArchives", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkFlowArchives",
                schema: "dbo");
        }
    }
}
