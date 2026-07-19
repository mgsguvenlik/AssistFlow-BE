using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "YkbWorkflowAttachmentsJson",
                schema: "ykb",
                table: "YkbWorkFlowArchive",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowAttachmentsJson",
                schema: "dbo",
                table: "WorkFlowArchives",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QnbWorkflowAttachmentsJson",
                schema: "qnb",
                table: "QnbWorkFlowArchive",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowAttachment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedStepCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LastUpdatedStepCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowAttachment", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAttachment_RequestNo",
                table: "WorkflowAttachment",
                column: "RequestNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowAttachment");

            migrationBuilder.DropColumn(
                name: "YkbWorkflowAttachmentsJson",
                schema: "ykb",
                table: "YkbWorkFlowArchive");

            migrationBuilder.DropColumn(
                name: "WorkflowAttachmentsJson",
                schema: "dbo",
                table: "WorkFlowArchives");

            migrationBuilder.DropColumn(
                name: "QnbWorkflowAttachmentsJson",
                schema: "qnb",
                table: "QnbWorkFlowArchive");
        }
    }
}
