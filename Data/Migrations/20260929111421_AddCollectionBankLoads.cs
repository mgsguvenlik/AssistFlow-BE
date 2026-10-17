using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionBankLoads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankBaseline",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceCount = table.Column<long>(type: "bigint", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankBaseline", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankLoad",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankLoad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankTransaction",
                schema: "collection",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    LoadRowId = table.Column<long>(type: "bigint", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransaction", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "BankLoadRow",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoadId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    SourceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Issue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ContractId = table.Column<long>(type: "bigint", nullable: true),
                    CurrencyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    AppliedUser = table.Column<long>(type: "bigint", nullable: true),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankLoadRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankLoadRow_BankLoad_LoadId",
                        column: x => x.LoadId,
                        principalSchema: "collection",
                        principalTable: "BankLoad",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankLoad_Type_FileHash_Period",
                schema: "collection",
                table: "BankLoad",
                columns: new[] { "Type", "FileHash", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankLoadRow_LoadId_RowNumber",
                schema: "collection",
                table: "BankLoadRow",
                columns: new[] { "LoadId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankLoadRow_LoadId_Status_RowNumber",
                schema: "collection",
                table: "BankLoadRow",
                columns: new[] { "LoadId", "Status", "RowNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankBaseline",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "BankLoadRow",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "BankTransaction",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "BankLoad",
                schema: "collection");
        }
    }
}
