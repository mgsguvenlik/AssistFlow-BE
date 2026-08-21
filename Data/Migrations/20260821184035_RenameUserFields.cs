using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameUserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TechnicianEmail",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "TechnicianPhone",
                table: "Users",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "TechnicianName",
                table: "Users",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "TechnicianEmail",
                table: "Users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "TechnicianCompany",
                table: "Users",
                newName: "Company");

            migrationBuilder.RenameColumn(
                name: "TechnicianCode",
                table: "Users",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "TechnicianAddress",
                table: "Users",
                newName: "Address");

            migrationBuilder.RenameIndex(
                name: "IX_Users_TechnicianCode",
                table: "Users",
                newName: "IX_Users_Code");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "Users",
                newName: "TechnicianPhone");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Users",
                newName: "TechnicianName");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Users",
                newName: "TechnicianEmail");

            migrationBuilder.RenameColumn(
                name: "Company",
                table: "Users",
                newName: "TechnicianCompany");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Users",
                newName: "TechnicianCode");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Users",
                newName: "TechnicianAddress");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Code",
                table: "Users",
                newName: "IX_Users_TechnicianCode");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TechnicianEmail",
                table: "Users",
                column: "TechnicianEmail",
                unique: true,
                filter: "[TechnicianEmail] IS NOT NULL");
        }
    }
}
