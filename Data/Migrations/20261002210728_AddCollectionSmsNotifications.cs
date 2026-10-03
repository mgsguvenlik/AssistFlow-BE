using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionSmsNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsNotification",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<long>(type: "bigint", nullable: false),
                    RatePeriodId = table.Column<long>(type: "bigint", nullable: false),
                    OldAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncreasePercent = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsNotification", x => x.Id);
                    table.CheckConstraint("CK_SmsNotification_Attempts", "[AttemptCount] > 0");
                    table.CheckConstraint("CK_SmsNotification_Status", "[Status] BETWEEN 0 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "SmsAttempt",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    RecipientCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Template = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsAutomatic = table.Column<bool>(type: "bit", nullable: false),
                    IsSimulation = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ResultMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PackageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    StartedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsAttempt", x => x.Id);
                    table.CheckConstraint("CK_SmsAttempt_Status", "[Status] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_SmsAttempt_SmsNotification_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "collection",
                        principalTable: "SmsNotification",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsAttempt_NotificationId_Sequence",
                schema: "collection",
                table: "SmsAttempt",
                columns: new[] { "NotificationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsAttempt_Status_CreatedDate_Id",
                schema: "collection",
                table: "SmsAttempt",
                columns: new[] { "Status", "CreatedDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsNotification_ContractId_CreatedDate_Id",
                schema: "collection",
                table: "SmsNotification",
                columns: new[] { "ContractId", "CreatedDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsNotification_RatePeriodId",
                schema: "collection",
                table: "SmsNotification",
                column: "RatePeriodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsNotification_Status_UpdatedDate_Id",
                schema: "collection",
                table: "SmsNotification",
                columns: new[] { "Status", "UpdatedDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsAttempt",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "SmsNotification",
                schema: "collection");
        }
    }
}
