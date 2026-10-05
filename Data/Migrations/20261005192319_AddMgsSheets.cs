using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMgsSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sheets");

            migrationBuilder.CreateTable(
                name: "Workbooks",
                schema: "sheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerId = table.Column<long>(type: "bigint", nullable: false),
                    ActiveRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbooks_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Access",
                schema: "sheets",
                columns: table => new
                {
                    WorkbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    GrantedBy = table.Column<long>(type: "bigint", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Access", x => new { x.WorkbookId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Access_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Access_Workbooks_WorkbookId",
                        column: x => x.WorkbookId,
                        principalSchema: "sheets",
                        principalTable: "Workbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                schema: "sheets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Workbooks_WorkbookId",
                        column: x => x.WorkbookId,
                        principalSchema: "sheets",
                        principalTable: "Workbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataBlocks",
                schema: "sheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CellsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataBlocks_Workbooks_WorkbookId",
                        column: x => x.WorkbookId,
                        principalSchema: "sheets",
                        principalTable: "Workbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Revisions",
                schema: "sheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Revisions_Workbooks_WorkbookId",
                        column: x => x.WorkbookId,
                        principalSchema: "sheets",
                        principalTable: "Workbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Access_UserId_WorkbookId",
                schema: "sheets",
                table: "Access",
                columns: new[] { "UserId", "WorkbookId" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_WorkbookId_OccurredAtUtc",
                schema: "sheets",
                table: "Activities",
                columns: new[] { "WorkbookId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DataBlocks_WorkbookId",
                schema: "sheets",
                table: "DataBlocks",
                column: "WorkbookId");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_WorkbookId_CreatedAtUtc",
                schema: "sheets",
                table: "Revisions",
                columns: new[] { "WorkbookId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbooks_OwnerId_UpdatedAtUtc",
                schema: "sheets",
                table: "Workbooks",
                columns: new[] { "OwnerId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbooks_UpdatedAtUtc",
                schema: "sheets",
                table: "Workbooks",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Access",
                schema: "sheets");

            migrationBuilder.DropTable(
                name: "Activities",
                schema: "sheets");

            migrationBuilder.DropTable(
                name: "DataBlocks",
                schema: "sheets");

            migrationBuilder.DropTable(
                name: "Revisions",
                schema: "sheets");

            migrationBuilder.DropTable(
                name: "Workbooks",
                schema: "sheets");
        }
    }
}
