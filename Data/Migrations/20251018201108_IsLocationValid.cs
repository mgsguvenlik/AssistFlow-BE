using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class IsLocationValid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocationValid",
                table: "WorkFlows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EndLocation",
                table: "TechnicalServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartLocation",
                table: "TechnicalServices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocationValid",
                table: "WorkFlows");

            migrationBuilder.DropColumn(
                name: "EndLocation",
                table: "TechnicalServices");

            migrationBuilder.DropColumn(
                name: "StartLocation",
                table: "TechnicalServices");
        }
    }
}
