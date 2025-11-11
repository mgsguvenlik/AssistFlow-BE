using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class roleModuleUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuRole_Modules_ModuleId",
                table: "MenuRole");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Modules",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "ModulId1",
                table: "MenuRole");

            migrationBuilder.RenameTable(
                name: "Modules",
                newName: "Menus");

            migrationBuilder.RenameIndex(
                name: "IX_Modules_Name",
                table: "Menus",
                newName: "IX_Menus_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Menus",
                table: "Menus",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuRole_Menus_MenuId",
                table: "MenuRole",
                column: "ModulId",
                principalTable: "Menus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuRole_Menus_MenuId",
                table: "MenuRole");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Menus",
                table: "Menus");

            migrationBuilder.RenameTable(
                name: "Menus",
                newName: "Modules");

            migrationBuilder.RenameIndex(
                name: "IX_Menus_Name",
                table: "Modules",
                newName: "IX_Modules_Name");

            migrationBuilder.AddColumn<long>(
                name: "ModulId1",
                table: "MenuRole",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Modules",
                table: "Modules",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuRole_Modules_ModuleId",
                table: "MenuRole",
                column: "ModulId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
