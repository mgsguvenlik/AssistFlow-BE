using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoice",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    LegacyInvoiceFollowId = table.Column<long>(type: "bigint", nullable: true),
                    LegacyCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    LegacyCreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LegacyModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LegacyCreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegacyModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceHash = table.Column<byte[]>(type: "binary(32)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoice", x => x.Id);
                    table.CheckConstraint("CK_Invoice_Type", "[Type] IN ('B', 'K')");
                    table.ForeignKey(
                        name: "FK_Invoice_CurrencyType_CurrencyTypeId",
                        column: x => x.CurrencyTypeId,
                        principalTable: "CurrencyType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoice_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoicePayment",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    LegacyInvoiceFollowPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    LegacyCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    LegacyCreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LegacyModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LegacyCreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegacyModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceHash = table.Column<byte[]>(type: "binary(32)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePayment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePayment_Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "collection",
                        principalTable: "Invoice",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_CurrencyTypeId",
                schema: "collection",
                table: "Invoice",
                column: "CurrencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_CustomerId_Date_Id",
                schema: "collection",
                table: "Invoice",
                columns: new[] { "CustomerId", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_LegacyInvoiceFollowId",
                schema: "collection",
                table: "Invoice",
                column: "LegacyInvoiceFollowId",
                unique: true,
                filter: "[LegacyInvoiceFollowId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Type_Date_Id",
                schema: "collection",
                table: "Invoice",
                columns: new[] { "Type", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayment_InvoiceId_Date_Id",
                schema: "collection",
                table: "InvoicePayment",
                columns: new[] { "InvoiceId", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayment_LegacyInvoiceFollowPaymentId",
                schema: "collection",
                table: "InvoicePayment",
                column: "LegacyInvoiceFollowPaymentId",
                unique: true,
                filter: "[LegacyInvoiceFollowPaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoicePayment",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "Invoice",
                schema: "collection");
        }
    }
}
