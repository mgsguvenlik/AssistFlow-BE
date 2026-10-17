using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionOriginalAnchorDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "OriginalAnchorDay",
                schema: "collection",
                table: "ContractRatePeriod",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContractRatePeriod_OriginalDay",
                schema: "collection",
                table: "ContractRatePeriod",
                sql: "[OriginalAnchorDay] IS NULL OR [OriginalAnchorDay] BETWEEN 1 AND 31");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ContractRatePeriod_OriginalDay",
                schema: "collection",
                table: "ContractRatePeriod");

            migrationBuilder.DropColumn(
                name: "OriginalAnchorDay",
                schema: "collection",
                table: "ContractRatePeriod");
        }
    }
}
