using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSubGroupFaz2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressApprovers_Customers_CustomerId1",
                table: "ProgressApprovers");

            migrationBuilder.RenameColumn(
                name: "CustomerId1",
                table: "ProgressApprovers",
                newName: "CustomerGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_ProgressApprovers_CustomerId1",
                table: "ProgressApprovers",
                newName: "IX_ProgressApprovers_CustomerGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId",
                table: "ProgressApprovers",
                column: "CustomerGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId",
                table: "ProgressApprovers");

            migrationBuilder.RenameColumn(
                name: "CustomerGroupId",
                table: "ProgressApprovers",
                newName: "CustomerId1");

            migrationBuilder.RenameIndex(
                name: "IX_ProgressApprovers_CustomerGroupId",
                table: "ProgressApprovers",
                newName: "IX_ProgressApprovers_CustomerId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressApprovers_Customers_CustomerId1",
                table: "ProgressApprovers",
                column: "CustomerId1",
                principalTable: "Customers",
                principalColumn: "Id");
        }
    }
}
