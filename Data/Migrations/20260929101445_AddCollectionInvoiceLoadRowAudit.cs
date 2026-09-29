using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionInvoiceLoadRowAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppliedAt",
                schema: "collection",
                table: "InvoiceLoadRow",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AppliedUser",
                schema: "collection",
                table: "InvoiceLoadRow",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedAt",
                schema: "collection",
                table: "InvoiceLoadRow");

            migrationBuilder.DropColumn(
                name: "AppliedUser",
                schema: "collection",
                table: "InvoiceLoadRow");
        }
    }
}
