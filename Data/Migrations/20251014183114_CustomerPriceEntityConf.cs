using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPriceEntityConf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "ServicesRequestProducts",
                type: "bigint",
                nullable: true);


            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequestProducts_CustomerId",
                table: "ServicesRequestProducts",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_Customers_CustomerId",
                table: "ServicesRequestProducts",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_Customers_CustomerId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropIndex(
                name: "IX_ServicesRequestProducts_CustomerId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "ServicesRequestProducts");
        }
    }
}
