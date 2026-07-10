using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class addWorkOrderToArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkOrderTypesJson",
                schema: "ykb",
                table: "YkbWorkFlowArchive",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderTypesJson",
                schema: "dbo",
                table: "WorkFlowArchives",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderTypesJson",
                schema: "qnb",
                table: "QnbWorkFlowArchive",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkOrderTypesJson",
                schema: "ykb",
                table: "YkbWorkFlowArchive");

            migrationBuilder.DropColumn(
                name: "WorkOrderTypesJson",
                schema: "dbo",
                table: "WorkFlowArchives");

            migrationBuilder.DropColumn(
                name: "WorkOrderTypesJson",
                schema: "qnb",
                table: "QnbWorkFlowArchive");
        }
    }
}
