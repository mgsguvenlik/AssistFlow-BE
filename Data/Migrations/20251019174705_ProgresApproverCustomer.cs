using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgresApproverCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId1",
                table: "ProgressApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ProgressApprovers_CustomerGroupId_Email",
                table: "ProgressApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ProgressApprovers_CustomerGroupId1",
                table: "ProgressApprovers");

            migrationBuilder.DropColumn(
                name: "CustomerGroupId1",
                table: "ProgressApprovers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerGroupId1",
                table: "ProgressApprovers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressApprovers_CustomerGroupId_Email",
                table: "ProgressApprovers",
                columns: new[] { "CustomerGroupId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressApprovers_CustomerGroupId1",
                table: "ProgressApprovers",
                column: "CustomerGroupId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId1",
                table: "ProgressApprovers",
                column: "CustomerGroupId1",
                principalTable: "CustomerGroups",
                principalColumn: "Id");
        }
    }
}
