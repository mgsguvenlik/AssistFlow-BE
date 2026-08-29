using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "helpdesk");

            migrationBuilder.AddColumn<string>(
                name: "InReplyTo",
                table: "MailOutboxes",
                type: "nvarchar(998)",
                maxLength: 998,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageId",
                table: "MailOutboxes",
                type: "nvarchar(998)",
                maxLength: 998,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "References",
                table: "MailOutboxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Mailbox",
                schema: "helpdesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ProtectedPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImapServer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImapPort = table.Column<int>(type: "int", nullable: false),
                    UseSsl = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mailbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketNumberSequence",
                schema: "helpdesk",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketNumberSequence", x => x.Year);
                });

            migrationBuilder.CreateTable(
                name: "MailRule",
                schema: "helpdesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MailboxId = table.Column<long>(type: "bigint", nullable: false),
                    Field = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LogicalOperator = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailRule_Mailbox_MailboxId",
                        column: x => x.MailboxId,
                        principalSchema: "helpdesk",
                        principalTable: "Mailbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ticket",
                schema: "helpdesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RequesterEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ToRecipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CcRecipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    IsSuspended = table.Column<bool>(type: "bit", nullable: false),
                    SuspendedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptanceMailSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    MailboxId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ticket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ticket_Mailbox_MailboxId",
                        column: x => x.MailboxId,
                        principalSchema: "helpdesk",
                        principalTable: "Mailbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketAssignment",
                schema: "helpdesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAssignment_Ticket_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "helpdesk",
                        principalTable: "Ticket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketAssignment_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketComment",
                schema: "helpdesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketComment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketComment_Ticket_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "helpdesk",
                        principalTable: "Ticket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketHistory",
                schema: "helpdesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketHistory_Ticket_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "helpdesk",
                        principalTable: "Ticket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketMail",
                schema: "helpdesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    MailboxId = table.Column<long>(type: "bigint", nullable: true),
                    MessageId = table.Column<string>(type: "nvarchar(998)", maxLength: 998, nullable: false),
                    InReplyTo = table.Column<string>(type: "nvarchar(998)", maxLength: 998, nullable: true),
                    References = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    ToRecipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CcRecipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MailDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMail_Ticket_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "helpdesk",
                        principalTable: "Ticket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskMailbox_IsActive",
                schema: "helpdesk",
                table: "Mailbox",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UX_HelpdeskMailbox_Address",
                schema: "helpdesk",
                table: "Mailbox",
                column: "Address",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskRule_Mailbox_Order",
                schema: "helpdesk",
                table: "MailRule",
                columns: new[] { "MailboxId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicket_List",
                schema: "helpdesk",
                table: "Ticket",
                columns: new[] { "Status", "IsSuspended", "Priority", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicket_SuspendedUntil",
                schema: "helpdesk",
                table: "Ticket",
                column: "SuspendedUntil");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_MailboxId",
                schema: "helpdesk",
                table: "Ticket",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "UX_HelpdeskTicket_TicketNo",
                schema: "helpdesk",
                table: "Ticket",
                column: "TicketNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskAssignment_User",
                schema: "helpdesk",
                table: "TicketAssignment",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_HelpdeskAssignment_ActiveTicketUser",
                schema: "helpdesk",
                table: "TicketAssignment",
                columns: new[] { "TicketId", "UserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskComment_Ticket_Date",
                schema: "helpdesk",
                table: "TicketComment",
                columns: new[] { "TicketId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskHistory_Ticket_Date",
                schema: "helpdesk",
                table: "TicketHistory",
                columns: new[] { "TicketId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskMail_Ticket_Date",
                schema: "helpdesk",
                table: "TicketMail",
                columns: new[] { "TicketId", "MailDate" });

            migrationBuilder.CreateIndex(
                name: "UX_HelpdeskMail_Mailbox_MessageId",
                schema: "helpdesk",
                table: "TicketMail",
                columns: new[] { "MailboxId", "MessageId" },
                unique: true,
                filter: "[MailboxId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailRule",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "TicketAssignment",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "TicketComment",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "TicketHistory",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "TicketMail",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "TicketNumberSequence",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "Ticket",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "Mailbox",
                schema: "helpdesk");

            migrationBuilder.DropColumn(
                name: "InReplyTo",
                table: "MailOutboxes");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "MailOutboxes");

            migrationBuilder.DropColumn(
                name: "References",
                table: "MailOutboxes");
        }
    }
}
