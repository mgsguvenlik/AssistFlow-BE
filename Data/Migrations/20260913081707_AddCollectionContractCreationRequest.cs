using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionContractCreationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CreationPayloadHash",
                schema: "collection",
                table: "Contract",
                type: "varbinary(32)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreationRequestId",
                schema: "collection",
                table: "Contract",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Contract_CreationRequestId",
                schema: "collection",
                table: "Contract",
                column: "CreationRequestId",
                unique: true,
                filter: "[CreationRequestId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contract_CreationRequest",
                schema: "collection",
                table: "Contract",
                sql: "([CreationRequestId] IS NULL AND [CreationPayloadHash] IS NULL) OR ([CreationRequestId] IS NOT NULL AND [CreationPayloadHash] IS NOT NULL AND DATALENGTH([CreationPayloadHash]) = 32)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Contract_CreationRequestId",
                schema: "collection",
                table: "Contract");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contract_CreationRequest",
                schema: "collection",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "CreationPayloadHash",
                schema: "collection",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "CreationRequestId",
                schema: "collection",
                table: "Contract");
        }
    }
}
