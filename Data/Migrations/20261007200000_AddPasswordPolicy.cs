using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations;

public partial class AddPasswordPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "MustChangePassword", table: "Users", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "PasswordChangedAt", table: "Users", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<int>(name: "PasswordVersion", table: "Users", type: "int", nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MustChangePassword", table: "Users");
        migrationBuilder.DropColumn(name: "PasswordChangedAt", table: "Users");
        migrationBuilder.DropColumn(name: "PasswordVersion", table: "Users");
    }
}
