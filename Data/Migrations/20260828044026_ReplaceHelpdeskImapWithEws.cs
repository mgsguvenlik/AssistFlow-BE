using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceHelpdeskImapWithEws : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapPort",
                schema: "helpdesk",
                table: "Mailbox");

            migrationBuilder.DropColumn(
                name: "UseSsl",
                schema: "helpdesk",
                table: "Mailbox");

            migrationBuilder.RenameColumn(
                name: "ImapServer",
                schema: "helpdesk",
                table: "Mailbox",
                newName: "EwsUrl");

            migrationBuilder.AlterColumn<string>(
                name: "EwsUrl",
                schema: "helpdesk",
                table: "Mailbox",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EwsUrl",
                schema: "helpdesk",
                table: "Mailbox",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.RenameColumn(
                name: "EwsUrl",
                schema: "helpdesk",
                table: "Mailbox",
                newName: "ImapServer");

            migrationBuilder.AddColumn<int>(
                name: "ImapPort",
                schema: "helpdesk",
                table: "Mailbox",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UseSsl",
                schema: "helpdesk",
                table: "Mailbox",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
