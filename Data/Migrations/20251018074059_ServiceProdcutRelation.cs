using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ServiceProdcutRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_Customers_CustomerId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_Product_ProductId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_ServicesRequests_ServicesRequestId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_Warehouses_WarehouseId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ServicesRequestProducts",
                table: "ServicesRequestProducts");

            migrationBuilder.DropIndex(
                name: "IX_ServicesRequestProducts_WarehouseId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "ServicesRequestProducts");

            migrationBuilder.RenameColumn(
                name: "ServicesRequestId",
                table: "ServicesRequestProducts",
                newName: "Id");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "ServicesRequestProducts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropColumn(
                 name: "Id",
                 table: "ServicesRequestProducts");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "ServicesRequestProducts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");


            migrationBuilder.AddColumn<string>(
                name: "RequestNo",
                table: "ServicesRequestProducts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServicesRequestProducts",
                table: "ServicesRequestProducts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_Customers_CustomerId",
                table: "ServicesRequestProducts",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_Product_ProductId",
                table: "ServicesRequestProducts",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_Customers_CustomerId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_ServicesRequestProducts_Product_ProductId",
                table: "ServicesRequestProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ServicesRequestProducts",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "RequestNo",
                table: "ServicesRequestProducts");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ServicesRequestProducts",
                newName: "ServicesRequestId");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "ServicesRequestProducts",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "ServicesRequestId",
                table: "ServicesRequestProducts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "WarehouseId",
                table: "ServicesRequestProducts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServicesRequestProducts",
                table: "ServicesRequestProducts",
                columns: new[] { "ServicesRequestId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequestProducts_WarehouseId",
                table: "ServicesRequestProducts",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_Customers_CustomerId",
                table: "ServicesRequestProducts",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_Product_ProductId",
                table: "ServicesRequestProducts",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_ServicesRequests_ServicesRequestId",
                table: "ServicesRequestProducts",
                column: "ServicesRequestId",
                principalTable: "ServicesRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServicesRequestProducts_Warehouses_WarehouseId",
                table: "ServicesRequestProducts",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }
    }
}
