using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class Ykb_Update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YkbServicesRequest_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_YkbServicesRequest_ServiceType_ServiceTypeId",
                schema: "ykb",
                table: "YkbServicesRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_YkbServicesRequestProduct_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequestProduct");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "ServiceTypeId",
                schema: "ykb",
                table: "YkbServicesRequest",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                schema: "ykb",
                table: "YkbServicesRequest",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerApprovedAt",
                schema: "ykb",
                table: "YkbFinalApproval",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomerApprovedBy",
                schema: "ykb",
                table: "YkbFinalApproval",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNote",
                schema: "ykb",
                table: "YkbFinalApproval",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_YkbServicesRequest_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_YkbServicesRequest_ServiceType_ServiceTypeId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "ServiceTypeId",
                principalTable: "ServiceType",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_YkbServicesRequestProduct_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YkbServicesRequest_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_YkbServicesRequest_ServiceType_ServiceTypeId",
                schema: "ykb",
                table: "YkbServicesRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_YkbServicesRequestProduct_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequestProduct");

            migrationBuilder.DropColumn(
                name: "CustomerApprovedAt",
                schema: "ykb",
                table: "YkbFinalApproval");

            migrationBuilder.DropColumn(
                name: "CustomerApprovedBy",
                schema: "ykb",
                table: "YkbFinalApproval");

            migrationBuilder.DropColumn(
                name: "CustomerNote",
                schema: "ykb",
                table: "YkbFinalApproval");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ServiceTypeId",
                schema: "ykb",
                table: "YkbServicesRequest",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                schema: "ykb",
                table: "YkbServicesRequest",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_YkbServicesRequest_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_YkbServicesRequest_ServiceType_ServiceTypeId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "ServiceTypeId",
                principalTable: "ServiceType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_YkbServicesRequestProduct_Customers_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
