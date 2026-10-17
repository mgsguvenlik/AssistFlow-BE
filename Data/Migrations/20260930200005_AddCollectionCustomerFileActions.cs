using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionCustomerFileActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "RetainedLegacyCustomerId",
                schema: "collection",
                table: "CustomerAttachment",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "LegacyCustomerId",
                schema: "collection",
                table: "CustomerAttachment",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "ArchiveHash",
                schema: "collection",
                table: "CustomerAttachment",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<long>(
                name: "CreatedUser",
                schema: "collection",
                table: "CustomerAttachment",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "collection",
                table: "CustomerAttachment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedDate",
                schema: "collection",
                table: "CustomerAttachment",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RemovedUser",
                schema: "collection",
                table: "CustomerAttachment",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedUser",
                schema: "collection",
                table: "CustomerAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "collection",
                table: "CustomerAttachment");

            migrationBuilder.DropColumn(
                name: "RemovedDate",
                schema: "collection",
                table: "CustomerAttachment");

            migrationBuilder.DropColumn(
                name: "RemovedUser",
                schema: "collection",
                table: "CustomerAttachment");

            migrationBuilder.AlterColumn<long>(
                name: "RetainedLegacyCustomerId",
                schema: "collection",
                table: "CustomerAttachment",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "LegacyCustomerId",
                schema: "collection",
                table: "CustomerAttachment",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ArchiveHash",
                schema: "collection",
                table: "CustomerAttachment",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
