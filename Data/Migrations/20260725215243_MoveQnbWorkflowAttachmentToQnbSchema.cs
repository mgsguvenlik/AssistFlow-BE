using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveQnbWorkflowAttachmentToQnbSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "QnbWorkflowAttachment",
                newName: "QnbWorkflowAttachment",
                newSchema: "qnb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "QnbWorkflowAttachment",
                schema: "qnb",
                newName: "QnbWorkflowAttachment");
        }
    }
}
