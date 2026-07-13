using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOzelFiyatliUrun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsServiceFeeProduct",
                table: "Product",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeePercentage",
                table: "Product",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsServiceFeeProduct",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ServiceFeePercentage",
                table: "Product");
        }
    }
}
