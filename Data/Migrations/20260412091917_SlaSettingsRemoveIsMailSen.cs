using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class SlaSettingsRemoveIsMailSen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMailSent",
                table: "WorkFlowSlaSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMailSent",
                table: "WorkFlowSlaSettings",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "Mail gönderildi mi");
        }
    }
}
