using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class addSlaSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkFlowSlaSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerType = table.Column<int>(type: "int", nullable: false, comment: "Müşteri/İş birimi tipi (General, Ykb, Individual, Corporate)"),
                    Priority = table.Column<int>(type: "int", nullable: false, comment: "İş akışı öncelik seviyesi"),
                    SlaDurationDays = table.Column<int>(type: "int", nullable: false, comment: "SLA süresi (gün)"),
                    NotificationBeforeDays = table.Column<int>(type: "int", nullable: false, comment: "Bildirim gönderilecek süre (gün önce)"),
                    NotificationEmails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "Bildirim gönderilecek e-posta adresleri (virgülle ayrılmış)"),
                    IsMailSent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "Mail gönderildi mi"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "Aktif mi"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "Açıklama"),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowSlaSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowSlaSettings_CustomerType_Priority",
                table: "WorkFlowSlaSettings",
                columns: new[] { "CustomerType", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlowSlaSettings_IsActive",
                table: "WorkFlowSlaSettings",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkFlowSlaSettings");
        }
    }
}
