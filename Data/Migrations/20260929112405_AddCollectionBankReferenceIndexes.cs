using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionBankReferenceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contract_GtsNo_Active",
                schema: "collection",
                table: "Contract",
                column: "GtsNo",
                filter: "[GtsNo] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_IvrNo_Active",
                schema: "collection",
                table: "Contract",
                column: "IvrNo",
                filter: "[IvrNo] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contract_GtsNo_Active",
                schema: "collection",
                table: "Contract");

            migrationBuilder.DropIndex(
                name: "IX_Contract_IvrNo_Active",
                schema: "collection",
                table: "Contract");
        }
    }
}
