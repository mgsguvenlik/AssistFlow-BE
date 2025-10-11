using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class customerProductGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_PriceGroups_PriceGroupId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "PriceGroups");

            migrationBuilder.RenameColumn(
                name: "PriceGroupId",
                table: "Customers",
                newName: "CustomerGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_PriceGroupId",
                table: "Customers",
                newName: "IX_Customers_CustomerGroupId");

            migrationBuilder.CreateTable(
                name: "CustomerGroupProductPrices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerGroupId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroupProductPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerGroupProductPrices_CustomerGroups_CustomerGroupId",
                        column: x => x.CustomerGroupId,
                        principalTable: "CustomerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerGroupProductPrices_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProductPrices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProductPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerProductPrices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerProductPrices_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupProductPrices_CustomerGroupId_ProductId",
                table: "CustomerGroupProductPrices",
                columns: new[] { "CustomerGroupId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupProductPrices_ProductId",
                table: "CustomerGroupProductPrices",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProductPrices_CustomerId_ProductId",
                table: "CustomerProductPrices",
                columns: new[] { "CustomerId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProductPrices_ProductId",
                table: "CustomerProductPrices",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId",
                table: "Customers",
                column: "CustomerGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "CustomerGroupProductPrices");

            migrationBuilder.DropTable(
                name: "CustomerProductPrices");

            migrationBuilder.RenameColumn(
                name: "CustomerGroupId",
                table: "Customers",
                newName: "PriceGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_CustomerGroupId",
                table: "Customers",
                newName: "IX_Customers_PriceGroupId");

            migrationBuilder.CreateTable(
                name: "PriceGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceGroups_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceGroups_ProductId",
                table: "PriceGroups",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_PriceGroups_PriceGroupId",
                table: "Customers",
                column: "PriceGroupId",
                principalTable: "PriceGroups",
                principalColumn: "Id");
        }
    }
}
