using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class saveProductPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CapturedAt",
                table: "ServicesRequestProducts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapturedCurrency",
                table: "ServicesRequestProducts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapturedSource",
                table: "ServicesRequestProducts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapturedTotal",
                table: "ServicesRequestProducts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapturedUnitPrice",
                table: "ServicesRequestProducts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPriceCaptured",
                table: "ServicesRequestProducts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapturedAt",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "CapturedCurrency",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "CapturedSource",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "CapturedTotal",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "CapturedUnitPrice",
                table: "ServicesRequestProducts");

            migrationBuilder.DropColumn(
                name: "IsPriceCaptured",
                table: "ServicesRequestProducts");
        }
    }
}
