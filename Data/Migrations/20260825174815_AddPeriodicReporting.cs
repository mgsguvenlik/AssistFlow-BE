using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodicReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeriodicReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SqlQuery = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputFormat = table.Column<int>(type: "int", nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NextRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodicReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodicReportExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodicReportId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: true),
                    OutputFormat = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    MailRecipientCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    TriggeredByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodicReportExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodicReportExecutions_PeriodicReports_PeriodicReportId",
                        column: x => x.PeriodicReportId,
                        principalTable: "PeriodicReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodicReportRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodicReportId = table.Column<long>(type: "bigint", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodicReportRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodicReportRecipients_PeriodicReports_PeriodicReportId",
                        column: x => x.PeriodicReportId,
                        principalTable: "PeriodicReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReportExecutions_Report_StartedAt",
                table: "PeriodicReportExecutions",
                columns: new[] { "PeriodicReportId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReportExecutions_Report_Status",
                table: "PeriodicReportExecutions",
                columns: new[] { "PeriodicReportId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_PeriodicReportRecipients_Report_Email",
                table: "PeriodicReportRecipients",
                columns: new[] { "PeriodicReportId", "EmailAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReports_Due",
                table: "PeriodicReports",
                columns: new[] { "IsActive", "IsDeleted", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PeriodicReports_Name_Active",
                table: "PeriodicReports",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PeriodicReportExecutions");

            migrationBuilder.DropTable(
                name: "PeriodicReportRecipients");

            migrationBuilder.DropTable(
                name: "PeriodicReports");
        }
    }
}
