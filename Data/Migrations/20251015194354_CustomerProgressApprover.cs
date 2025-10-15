using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerProgressApprover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId",
                table: "ProgressApprovers");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgressApprovers_Customers_CustomerId",
                table: "ProgressApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ProgressApprovers_CustomerId",
                table: "ProgressApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ProgressApprovers_CustomerId_Email",
                table: "ProgressApprovers");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "ProgressApprovers");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerGroupId",
                table: "ProgressApprovers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

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
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId",
                table: "ProgressApprovers",
                column: "CustomerGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId1",
                table: "ProgressApprovers",
                column: "CustomerGroupId1",
                principalTable: "CustomerGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId",
                table: "ProgressApprovers");

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

            migrationBuilder.AlterColumn<long>(
                name: "CustomerGroupId",
                table: "ProgressApprovers",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "ProgressApprovers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressApprovers_CustomerId",
                table: "ProgressApprovers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressApprovers_CustomerId_Email",
                table: "ProgressApprovers",
                columns: new[] { "CustomerId", "Email" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressApprovers_CustomerGroups_CustomerGroupId",
                table: "ProgressApprovers",
                column: "CustomerGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressApprovers_Customers_CustomerId",
                table: "ProgressApprovers",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
