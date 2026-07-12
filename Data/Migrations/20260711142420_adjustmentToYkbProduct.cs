using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class adjustmentToYkbProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppliedPriceAdjustmentValue",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPriceAdjustmentApplied",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedPriceAdjustmentValue",
                schema: "ykb",
                table: "YkbServicesRequestProduct");

            migrationBuilder.DropColumn(
                name: "IsPriceAdjustmentApplied",
                schema: "ykb",
                table: "YkbServicesRequestProduct");
        }
    }
}
