using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSubGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ParentGroupId",
                table: "CustomerGroups",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroups_ParentGroupId",
                table: "CustomerGroups",
                column: "ParentGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerGroups_CustomerGroups_ParentGroupId",
                table: "CustomerGroups",
                column: "ParentGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerGroups_CustomerGroups_ParentGroupId",
                table: "CustomerGroups");

            migrationBuilder.DropIndex(
                name: "IX_CustomerGroups_ParentGroupId",
                table: "CustomerGroups");

            migrationBuilder.DropColumn(
                name: "ParentGroupId",
                table: "CustomerGroups");
        }
    }
}
