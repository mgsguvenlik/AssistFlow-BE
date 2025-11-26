using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class roleModuleUpdate1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuRole_Menus_MenuId",
                table: "MenuRole");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuRole_Roles_RoleId1",
                table: "MenuRole");

            migrationBuilder.DropIndex(
                name: "IX_MenuRole_RoleId_ModulId",
                table: "MenuRole");

            migrationBuilder.DropIndex(
                name: "IX_MenuRole_RoleId1",
                table: "MenuRole");

            migrationBuilder.DropColumn(
                name: "RoleId1",
                table: "MenuRole");

            migrationBuilder.CreateIndex(
                name: "IX_MenuRole_RoleId",
                table: "MenuRole",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuRole_Menus_ModulId",
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
                name: "FK_MenuRole_Menus_ModulId",
                table: "MenuRole");

            migrationBuilder.DropIndex(
                name: "IX_MenuRole_RoleId",
                table: "MenuRole");

            migrationBuilder.AddColumn<long>(
                name: "RoleId1",
                table: "MenuRole",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuRole_RoleId_ModulId",
                table: "MenuRole",
                columns: new[] { "RoleId", "ModulId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuRole_RoleId1",
                table: "MenuRole",
                column: "RoleId1");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuRole_Menus_MenuId",
                table: "MenuRole",
                column: "ModulId",
                principalTable: "Menus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuRole_Roles_RoleId1",
                table: "MenuRole",
                column: "RoleId1",
                principalTable: "Roles",
                principalColumn: "Id");
        }
    }
}
