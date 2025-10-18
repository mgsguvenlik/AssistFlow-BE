using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class Customer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CustomerMainGroupName",
                table: "Customers",
                newName: "District");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InstallationDate",
                table: "Customers",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstallationDate",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "District",
                table: "Customers",
                newName: "CustomerMainGroupName");
        }
    }
}
