using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmPurchaseRequestModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "PurchaseRequestStep",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrderNo = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestStep", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequest",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    RequesterUserId = table.Column<long>(type: "bigint", nullable: false),
                    ManagerUserId = table.Column<long>(type: "bigint", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    IsOfficePurchase = table.Column<bool>(type: "bit", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    SystemTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentStepId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_PurchaseRequestStep_CurrentStepId",
                        column: x => x.CurrentStepId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_SystemType_SystemTypeId",
                        column: x => x.SystemTypeId,
                        principalTable: "SystemType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_Users_ManagerUserId",
                        column: x => x.ManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestAction",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRequestStepId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetStepId = table.Column<long>(type: "bigint", nullable: true),
                    RequiresDescription = table.Column<bool>(type: "bit", nullable: false),
                    OrderNo = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestAction_PurchaseRequestStep_PurchaseRequestStepId",
                        column: x => x.PurchaseRequestStepId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestAction_PurchaseRequestStep_TargetStepId",
                        column: x => x.TargetStepId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseAttachment",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRequestId = table.Column<long>(type: "bigint", nullable: false),
                    AttachmentType = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedStepId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseAttachment_PurchaseRequestStep_UploadedStepId",
                        column: x => x.UploadedStepId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchaseAttachment_PurchaseRequest_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestItem",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRequestId = table.Column<long>(type: "bigint", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BrandName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AlternateProductId = table.Column<long>(type: "bigint", nullable: true),
                    AlternateProductName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupplierListPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SupplierDiscountRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SupplierNetPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Maturity = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    RequiresWarehouseControl = table.Column<bool>(type: "bit", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestItem_CurrencyType_CurrencyTypeId",
                        column: x => x.CurrencyTypeId,
                        principalTable: "CurrencyType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestItem_Product_AlternateProductId",
                        column: x => x.AlternateProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestItem_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestItem_PurchaseRequest_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestTask",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRequestId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseRequestStepId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedUserId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedRoleId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestTask", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestTask_PurchaseRequestStep_PurchaseRequestStepId",
                        column: x => x.PurchaseRequestStepId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestTask_PurchaseRequest_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestTask_Roles_AssignedRoleId",
                        column: x => x.AssignedRoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestTask_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestTask_Users_CompletedUserId",
                        column: x => x.CompletedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestHistory",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRequestId = table.Column<long>(type: "bigint", nullable: false),
                    FromStepId = table.Column<long>(type: "bigint", nullable: true),
                    ToStepId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseRequestActionId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestHistory_PurchaseRequestAction_PurchaseRequestActionId",
                        column: x => x.PurchaseRequestActionId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestAction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestHistory_PurchaseRequestStep_FromStepId",
                        column: x => x.FromStepId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestHistory_PurchaseRequestStep_ToStepId",
                        column: x => x.ToStepId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequestStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestHistory_PurchaseRequest_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalSchema: "crm",
                        principalTable: "PurchaseRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAttachment_RequestId_Type",
                schema: "crm",
                table: "PurchaseAttachment",
                columns: new[] { "PurchaseRequestId", "AttachmentType" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAttachment_StoredFileName",
                schema: "crm",
                table: "PurchaseAttachment",
                column: "StoredFileName");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAttachment_UploadedStepId",
                schema: "crm",
                table: "PurchaseAttachment",
                column: "UploadedStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_CreatedDate",
                schema: "crm",
                table: "PurchaseRequest",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_CurrentStepId",
                schema: "crm",
                table: "PurchaseRequest",
                column: "CurrentStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_CustomerId",
                schema: "crm",
                table: "PurchaseRequest",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_ManagerUserId",
                schema: "crm",
                table: "PurchaseRequest",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_RequesterUserId",
                schema: "crm",
                table: "PurchaseRequest",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_RequestNo",
                schema: "crm",
                table: "PurchaseRequest",
                column: "RequestNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_Status",
                schema: "crm",
                table: "PurchaseRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_Status_CurrentStepId",
                schema: "crm",
                table: "PurchaseRequest",
                columns: new[] { "Status", "CurrentStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_SystemTypeId",
                schema: "crm",
                table: "PurchaseRequest",
                column: "SystemTypeId");


            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestAction_StepId_Code",
                schema: "crm",
                table: "PurchaseRequestAction",
                columns: new[] { "PurchaseRequestStepId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestAction_StepId_IsActive_OrderNo",
                schema: "crm",
                table: "PurchaseRequestAction",
                columns: new[] { "PurchaseRequestStepId", "IsActive", "OrderNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestAction_TargetStepId",
                schema: "crm",
                table: "PurchaseRequestAction",
                column: "TargetStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestHistory_ActionId",
                schema: "crm",
                table: "PurchaseRequestHistory",
                column: "PurchaseRequestActionId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestHistory_FromStepId",
                schema: "crm",
                table: "PurchaseRequestHistory",
                column: "FromStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestHistory_RequestId_CreatedDate",
                schema: "crm",
                table: "PurchaseRequestHistory",
                columns: new[] { "PurchaseRequestId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestHistory_ToStepId",
                schema: "crm",
                table: "PurchaseRequestHistory",
                column: "ToStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestItem_AlternateProductId",
                schema: "crm",
                table: "PurchaseRequestItem",
                column: "AlternateProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestItem_CurrencyTypeId",
                schema: "crm",
                table: "PurchaseRequestItem",
                column: "CurrencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestItem_ProductId",
                schema: "crm",
                table: "PurchaseRequestItem",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestItem_RequestId_LineNo",
                schema: "crm",
                table: "PurchaseRequestItem",
                columns: new[] { "PurchaseRequestId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestStep_Code",
                schema: "crm",
                table: "PurchaseRequestStep",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestStep_IsActive_OrderNo",
                schema: "crm",
                table: "PurchaseRequestStep",
                columns: new[] { "IsActive", "OrderNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestTask_AssignedRoleId_Status",
                schema: "crm",
                table: "PurchaseRequestTask",
                columns: new[] { "AssignedRoleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestTask_AssignedUserId_Status",
                schema: "crm",
                table: "PurchaseRequestTask",
                columns: new[] { "AssignedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestTask_CompletedUserId",
                schema: "crm",
                table: "PurchaseRequestTask",
                column: "CompletedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestTask_PurchaseRequestStepId",
                schema: "crm",
                table: "PurchaseRequestTask",
                column: "PurchaseRequestStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestTask_RequestId_Status",
                schema: "crm",
                table: "PurchaseRequestTask",
                columns: new[] { "PurchaseRequestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseRequestTask_RequestId_StepId_Pending",
                schema: "crm",
                table: "PurchaseRequestTask",
                columns: new[] { "PurchaseRequestId", "PurchaseRequestStepId" },
                unique: true,
                filter: "[Status] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseAttachment",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "PurchaseRequestHistory",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "PurchaseRequestItem",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "PurchaseRequestTask",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "PurchaseRequestAction",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "PurchaseRequest",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "PurchaseRequestStep",
                schema: "crm");
        }
    }
}
