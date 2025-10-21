using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgressApprovalName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionDate",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "InstallationDate",
                table: "Product");

            migrationBuilder.AddColumn<string>(
                name: "ApproverTechnicianName",
                table: "WorkFlows",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApproverTechnicianName",
                table: "WorkFlows");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConnectionDate",
                table: "Product",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InstallationDate",
                table: "Product",
                type: "datetimeoffset",
                nullable: true);
        }
    }
}
