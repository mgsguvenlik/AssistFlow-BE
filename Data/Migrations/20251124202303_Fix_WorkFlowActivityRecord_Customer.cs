using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class Fix_WorkFlowActivityRecord_Customer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "WorkFlowActivityRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowActivityRecords_CustomerId",
                table: "WorkFlowActivityRecords",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkFlowActivityRecords_Customers_CustomerId",
                table: "WorkFlowActivityRecords",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkFlowActivityRecords_Customers_CustomerId",
                table: "WorkFlowActivityRecords");

            migrationBuilder.DropIndex(
                name: "IX_WorkFlowActivityRecords_CustomerId",
                table: "WorkFlowActivityRecords");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "WorkFlowActivityRecords");
        }
    }
}
