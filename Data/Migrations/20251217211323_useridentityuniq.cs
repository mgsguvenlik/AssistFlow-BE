using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class useridentityuniq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_TechnicianCode",
                table: "Users",
                column: "TechnicianCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TechnicianEmail",
                table: "Users",
                column: "TechnicianEmail",
                unique: true,
                filter: "[TechnicianEmail] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TechnicianCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TechnicianEmail",
                table: "Users");
        }
    }
}
