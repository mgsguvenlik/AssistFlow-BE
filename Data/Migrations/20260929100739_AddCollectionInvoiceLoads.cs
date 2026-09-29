using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionInvoiceLoads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceAccount",
                schema: "collection",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    Issue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceAccount", x => x.Code);
                    table.ForeignKey(
                        name: "FK_InvoiceAccount_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLoad",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AppliedUser = table.Column<long>(type: "bigint", nullable: true),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLoad", x => x.Id);
                    table.CheckConstraint("CK_InvoiceLoad_Type", "[Type] IN ('B','K')");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLoadRow",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoadId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    SourceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    CurrencyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Issue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    ImportedKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLoadRow", x => x.Id);
                    table.CheckConstraint("CK_InvoiceLoadRow_Status", "[Status] IN ('Ready','Error','Duplicate','Imported')");
                    table.ForeignKey(
                        name: "FK_InvoiceLoadRow_InvoiceLoad_LoadId",
                        column: x => x.LoadId,
                        principalSchema: "collection",
                        principalTable: "InvoiceLoad",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAccount_CustomerId",
                schema: "collection",
                table: "InvoiceAccount",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLoad_CreatedDate_Id",
                schema: "collection",
                table: "InvoiceLoad",
                columns: new[] { "CreatedDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLoad_Type_FileHash",
                schema: "collection",
                table: "InvoiceLoad",
                columns: new[] { "Type", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLoadRow_ImportedKey",
                schema: "collection",
                table: "InvoiceLoadRow",
                column: "ImportedKey",
                unique: true,
                filter: "[ImportedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLoadRow_LoadId_RowNumber",
                schema: "collection",
                table: "InvoiceLoadRow",
                columns: new[] { "LoadId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLoadRow_LoadId_Status_RowNumber",
                schema: "collection",
                table: "InvoiceLoadRow",
                columns: new[] { "LoadId", "Status", "RowNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceAccount",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "InvoiceLoadRow",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "InvoiceLoad",
                schema: "collection");
        }
    }
}
