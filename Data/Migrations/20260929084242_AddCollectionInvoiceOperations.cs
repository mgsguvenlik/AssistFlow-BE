using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionInvoiceOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceOperation",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    LegacyInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    LegacyPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceOperation", x => x.Id);
                    table.CheckConstraint("CK_InvoiceOperation_After", "[AfterJson] IS NULL OR ISJSON([AfterJson]) = 1");
                    table.CheckConstraint("CK_InvoiceOperation_Before", "[BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1");
                    table.CheckConstraint("CK_InvoiceOperation_Kind", "[Kind] IN ('PaymentCreate','PaymentUpdate','PaymentDelete','CommentUpdate','InvoiceDelete')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOperation_InvoiceId_CompletedDate_Id",
                schema: "collection",
                table: "InvoiceOperation",
                columns: new[] { "InvoiceId", "CompletedDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOperation_LegacyInvoiceId_Kind",
                schema: "collection",
                table: "InvoiceOperation",
                columns: new[] { "LegacyInvoiceId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOperation_LegacyPaymentId_Kind",
                schema: "collection",
                table: "InvoiceOperation",
                columns: new[] { "LegacyPaymentId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOperation_RequestId",
                schema: "collection",
                table: "InvoiceOperation",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceOperation",
                schema: "collection");
        }
    }
}
