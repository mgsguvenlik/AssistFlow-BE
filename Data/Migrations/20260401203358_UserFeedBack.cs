using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class UserFeedBack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFeedbacks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    FeedbackType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AdminResponse = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponseDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RespondedBy = table.Column<long>(type: "bigint", nullable: true),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RelatedUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttachmentUrls = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedbacks_CreatedDate",
                table: "UserFeedbacks",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedbacks_CreatedUser",
                table: "UserFeedbacks",
                column: "CreatedUser");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedbacks_FeedbackType",
                table: "UserFeedbacks",
                column: "FeedbackType");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedbacks_Status",
                table: "UserFeedbacks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedbacks_Status_FeedbackType",
                table: "UserFeedbacks",
                columns: new[] { "Status", "FeedbackType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFeedbacks");
        }
    }
}
