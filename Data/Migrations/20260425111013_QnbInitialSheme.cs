using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class QnbInitialSheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "qnb");

            migrationBuilder.CreateTable(
                name: "QnbCustomerForm",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QnbServiceTrackNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServicesDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerApproverId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbCustomerForm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbCustomerForm_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QnbCustomerForm_ProgressApprovers_CustomerApproverId",
                        column: x => x.CustomerApproverId,
                        principalTable: "ProgressApprovers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QnbFinalApproval",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedBy = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    CustomerApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbFinalApproval", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QnbPricing",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbPricing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QnbServicesRequestProduct",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    IsPriceCaptured = table.Column<bool>(type: "bit", nullable: false),
                    CapturedUnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CapturedCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CapturedTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CapturedSource = table.Column<int>(type: "int", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbServicesRequestProduct", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbServicesRequestProduct_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QnbServicesRequestProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QnbTechnicalService",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ServiceTypeId = table.Column<long>(type: "bigint", nullable: true),
                    StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProblemDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolutionAndActions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Longitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLocationCheckRequired = table.Column<bool>(type: "bit", nullable: false),
                    ServicesStatus = table.Column<int>(type: "int", nullable: false),
                    ServicesCostStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbTechnicalService", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbTechnicalService_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QnbWarehouse",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeliveryDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarehouseStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbWarehouse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QnbWorkFlowArchive",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchiveReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbServicesRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbServicesRequestProductsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproverTechnicianJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerApproverJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbWorkFlowJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbWorkFlowReviewLogsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbTechnicalServiceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbTechnicalServiceImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbTechnicalServiceFormImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbWarehouseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbPricingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QnbFinalApprovalJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbWorkFlowArchive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QnbWorkFlowReviewLog",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QnbWorkFlowId = table.Column<long>(type: "bigint", nullable: false),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromStepId = table.Column<long>(type: "bigint", nullable: true),
                    FromStepCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToStepId = table.Column<long>(type: "bigint", nullable: true),
                    ToStepCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbWorkFlowReviewLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QnbWorkFlowStep",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbWorkFlowStep", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QnbTechnicalServiceFormImage",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QnbTechnicalServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbTechnicalServiceFormImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbTechnicalServiceFormImage_QnbTechnicalService_QnbTechnicalServiceId",
                        column: x => x.QnbTechnicalServiceId,
                        principalSchema: "qnb",
                        principalTable: "QnbTechnicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QnbTechnicalServiceImage",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QnbTechnicalServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbTechnicalServiceImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbTechnicalServiceImage_QnbTechnicalService_QnbTechnicalServiceId",
                        column: x => x.QnbTechnicalServiceId,
                        principalSchema: "qnb",
                        principalTable: "QnbTechnicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QnbServicesRequest",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QnbServiceTrackNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ServicesDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlannedCompletionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ServicesCostStatus = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsProductRequirement = table.Column<bool>(type: "bit", nullable: false),
                    QnbWorkFlowStepId = table.Column<long>(type: "bigint", nullable: true),
                    WorkFlowStepId = table.Column<long>(type: "bigint", nullable: true),
                    IsMailSended = table.Column<bool>(type: "bit", nullable: false),
                    CustomerApproverId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ServicesRequestStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbServicesRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbServicesRequest_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QnbServicesRequest_ProgressApprovers_CustomerApproverId",
                        column: x => x.CustomerApproverId,
                        principalTable: "ProgressApprovers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QnbServicesRequest_QnbWorkFlowStep_QnbWorkFlowStepId",
                        column: x => x.QnbWorkFlowStepId,
                        principalSchema: "qnb",
                        principalTable: "QnbWorkFlowStep",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QnbServicesRequest_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QnbWorkFlow",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestTitle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentStepId = table.Column<long>(type: "bigint", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsAgreement = table.Column<bool>(type: "bit", nullable: true),
                    IsLocationValid = table.Column<bool>(type: "bit", nullable: false),
                    CustomerApproverName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkFlowStatus = table.Column<int>(type: "int", nullable: false),
                    ApproverTechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbWorkFlow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbWorkFlow_QnbWorkFlowStep_CurrentStepId",
                        column: x => x.CurrentStepId,
                        principalSchema: "qnb",
                        principalTable: "QnbWorkFlowStep",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QnbWorkFlow_Users_ApproverTechnicianId",
                        column: x => x.ApproverTechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QnbWorkFlowActivityRecord",
                schema: "qnb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActionType = table.Column<short>(type: "smallint", nullable: false),
                    FromStepCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ToStepCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PerformedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WorkFlowId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QnbWorkFlowActivityRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QnbWorkFlowActivityRecord_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QnbWorkFlowActivityRecord_QnbWorkFlow_WorkFlowId",
                        column: x => x.WorkFlowId,
                        principalSchema: "qnb",
                        principalTable: "QnbWorkFlow",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_QnbCustomerForm_CustomerApproverId",
                schema: "qnb",
                table: "QnbCustomerForm",
                column: "CustomerApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbCustomerForm_CustomerId",
                schema: "qnb",
                table: "QnbCustomerForm",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbCustomerForm_RequestNo",
                schema: "qnb",
                table: "QnbCustomerForm",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequest_CustomerApproverId",
                schema: "qnb",
                table: "QnbServicesRequest",
                column: "CustomerApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequest_CustomerId",
                schema: "qnb",
                table: "QnbServicesRequest",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequest_QnbWorkFlowStepId",
                schema: "qnb",
                table: "QnbServicesRequest",
                column: "QnbWorkFlowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequest_RequestNo",
                schema: "qnb",
                table: "QnbServicesRequest",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequest_ServiceTypeId",
                schema: "qnb",
                table: "QnbServicesRequest",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequestProduct_CustomerId",
                schema: "qnb",
                table: "QnbServicesRequestProduct",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbServicesRequestProduct_ProductId",
                schema: "qnb",
                table: "QnbServicesRequestProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbTechnicalService_ServiceTypeId",
                schema: "qnb",
                table: "QnbTechnicalService",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbTechnicalServiceFormImage_QnbTechnicalServiceId",
                schema: "qnb",
                table: "QnbTechnicalServiceFormImage",
                column: "QnbTechnicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbTechnicalServiceImage_QnbTechnicalServiceId",
                schema: "qnb",
                table: "QnbTechnicalServiceImage",
                column: "QnbTechnicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbWorkFlow_ApproverTechnicianId",
                schema: "qnb",
                table: "QnbWorkFlow",
                column: "ApproverTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbWorkFlow_CurrentStepId",
                schema: "qnb",
                table: "QnbWorkFlow",
                column: "CurrentStepId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbWorkFlow_RequestNo",
                schema: "qnb",
                table: "QnbWorkFlow",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_QnbWorkFlowActivityRecord_CustomerId",
                schema: "qnb",
                table: "QnbWorkFlowActivityRecord",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_QnbWorkFlowActivityRecord_WorkFlowId",
                schema: "qnb",
                table: "QnbWorkFlowActivityRecord",
                column: "WorkFlowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QnbCustomerForm",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbFinalApproval",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbPricing",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbServicesRequest",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbServicesRequestProduct",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbTechnicalServiceFormImage",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbTechnicalServiceImage",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbWarehouse",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbWorkFlowActivityRecord",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbWorkFlowArchive",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbWorkFlowReviewLog",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbTechnicalService",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbWorkFlow",
                schema: "qnb");

            migrationBuilder.DropTable(
                name: "QnbWorkFlowStep",
                schema: "qnb");
        }
    }
}
