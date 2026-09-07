using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEkbTenantModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ekb");

            migrationBuilder.CreateTable(
                name: "EkbAccountingProcesses",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkbAccountingProcesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbCustomerForm",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EkbServiceTrackNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_EkbCustomerForm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbCustomerForm_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EkbCustomerForm_ProgressApprovers_CustomerApproverId",
                        column: x => x.CustomerApproverId,
                        principalTable: "ProgressApprovers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EkbFinalApproval",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbFinalApproval", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbPricing",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbPricing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbServicesRequestProduct",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbServicesRequestProduct", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbServicesRequestProduct_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EkbServicesRequestProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EkbTechnicalService",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbTechnicalService", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbTechnicalService_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EkbTechnicalServiceWorkSessions",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkFlowId = table.Column<long>(type: "bigint", nullable: false),
                    TechnicalServiceId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    SerialNo = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlannedEndAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ExtendCount = table.Column<int>(type: "int", nullable: false),
                    ManitouLogSequence = table.Column<long>(type: "bigint", nullable: true),
                    HasMissingZoneOnFinish = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedZonesText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MissingZonesText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinishDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkbTechnicalServiceWorkSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbWarehouse",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbWarehouse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbWorkFlowArchive",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchiveReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbServicesRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbServicesRequestProductsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproverTechnicianJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerApproverJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbWorkFlowJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbWorkFlowReviewLogsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbTechnicalServiceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbTechnicalServiceImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbTechnicalServiceFormImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbWarehouseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbPricingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbFinalApprovalJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EkbWorkflowAttachmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkbWorkFlowArchive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbWorkflowAttachment",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbWorkflowAttachment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbWorkFlowReviewLog",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EkbWorkFlowId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_EkbWorkFlowReviewLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbWorkFlowStep",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbWorkFlowStep", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkbTechnicalServiceFormImage",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EkbTechnicalServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkbTechnicalServiceFormImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbTechnicalServiceFormImage_EkbTechnicalService_EkbTechnicalServiceId",
                        column: x => x.EkbTechnicalServiceId,
                        principalSchema: "ekb",
                        principalTable: "EkbTechnicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EkbTechnicalServiceImage",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EkbTechnicalServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkbTechnicalServiceImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbTechnicalServiceImage_EkbTechnicalService_EkbTechnicalServiceId",
                        column: x => x.EkbTechnicalServiceId,
                        principalSchema: "ekb",
                        principalTable: "EkbTechnicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EkbServicesRequest",
                schema: "ekb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EkbServiceTrackNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ServicesDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlannedCompletionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ServicesCostStatus = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsProductRequirement = table.Column<bool>(type: "bit", nullable: false),
                    EkbWorkFlowStepId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_EkbServicesRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbServicesRequest_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EkbServicesRequest_EkbWorkFlowStep_EkbWorkFlowStepId",
                        column: x => x.EkbWorkFlowStepId,
                        principalSchema: "ekb",
                        principalTable: "EkbWorkFlowStep",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EkbServicesRequest_ProgressApprovers_CustomerApproverId",
                        column: x => x.CustomerApproverId,
                        principalTable: "ProgressApprovers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EkbServicesRequest_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EkbWorkFlow",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbWorkFlow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbWorkFlow_EkbWorkFlowStep_CurrentStepId",
                        column: x => x.CurrentStepId,
                        principalSchema: "ekb",
                        principalTable: "EkbWorkFlowStep",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EkbWorkFlow_Users_ApproverTechnicianId",
                        column: x => x.ApproverTechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EkbServicesRequestWorkOrderTypes",
                schema: "ekb",
                columns: table => new
                {
                    EkbServicesRequestId = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkbServicesRequestWorkOrderTypes", x => new { x.EkbServicesRequestId, x.WorkOrderTypeId });
                    table.ForeignKey(
                        name: "FK_EkbServicesRequestWorkOrderTypes_EkbServicesRequest_EkbServicesRequestId",
                        column: x => x.EkbServicesRequestId,
                        principalSchema: "ekb",
                        principalTable: "EkbServicesRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EkbServicesRequestWorkOrderTypes_WorkOrderTypes_WorkOrderTypeId",
                        column: x => x.WorkOrderTypeId,
                        principalTable: "WorkOrderTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EkbWorkFlowActivityRecord",
                schema: "ekb",
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
                    table.PrimaryKey("PK_EkbWorkFlowActivityRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkbWorkFlowActivityRecord_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EkbWorkFlowActivityRecord_EkbWorkFlow_WorkFlowId",
                        column: x => x.WorkFlowId,
                        principalSchema: "ekb",
                        principalTable: "EkbWorkFlow",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkbAccountingProcesses_IsProcessed",
                schema: "ekb",
                table: "EkbAccountingProcesses",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "IX_EkbAccountingProcesses_RequestNo",
                schema: "ekb",
                table: "EkbAccountingProcesses",
                column: "RequestNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkbCustomerForm_CustomerApproverId",
                schema: "ekb",
                table: "EkbCustomerForm",
                column: "CustomerApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbCustomerForm_CustomerId",
                schema: "ekb",
                table: "EkbCustomerForm",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbCustomerForm_RequestNo",
                schema: "ekb",
                table: "EkbCustomerForm",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequest_CustomerApproverId",
                schema: "ekb",
                table: "EkbServicesRequest",
                column: "CustomerApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequest_CustomerId",
                schema: "ekb",
                table: "EkbServicesRequest",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequest_EkbWorkFlowStepId",
                schema: "ekb",
                table: "EkbServicesRequest",
                column: "EkbWorkFlowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequest_RequestNo",
                schema: "ekb",
                table: "EkbServicesRequest",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequest_ServiceTypeId",
                schema: "ekb",
                table: "EkbServicesRequest",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequestProduct_CustomerId",
                schema: "ekb",
                table: "EkbServicesRequestProduct",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequestProduct_ProductId",
                schema: "ekb",
                table: "EkbServicesRequestProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbServicesRequestWorkOrderTypes_WorkOrderTypeId",
                schema: "ekb",
                table: "EkbServicesRequestWorkOrderTypes",
                column: "WorkOrderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbTechnicalService_ServiceTypeId",
                schema: "ekb",
                table: "EkbTechnicalService",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbTechnicalServiceFormImage_EkbTechnicalServiceId",
                schema: "ekb",
                table: "EkbTechnicalServiceFormImage",
                column: "EkbTechnicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbTechnicalServiceImage_EkbTechnicalServiceId",
                schema: "ekb",
                table: "EkbTechnicalServiceImage",
                column: "EkbTechnicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbWorkFlow_ApproverTechnicianId",
                schema: "ekb",
                table: "EkbWorkFlow",
                column: "ApproverTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbWorkFlow_CurrentStepId",
                schema: "ekb",
                table: "EkbWorkFlow",
                column: "CurrentStepId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbWorkFlow_RequestNo",
                schema: "ekb",
                table: "EkbWorkFlow",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_EkbWorkFlowActivityRecord_CustomerId",
                schema: "ekb",
                table: "EkbWorkFlowActivityRecord",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbWorkFlowActivityRecord_WorkFlowId",
                schema: "ekb",
                table: "EkbWorkFlowActivityRecord",
                column: "WorkFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_EkbWorkflowAttachment_RequestNo",
                schema: "ekb",
                table: "EkbWorkflowAttachment",
                column: "RequestNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkbAccountingProcesses",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbCustomerForm",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbFinalApproval",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbPricing",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbServicesRequestProduct",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbServicesRequestWorkOrderTypes",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbTechnicalServiceFormImage",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbTechnicalServiceImage",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbTechnicalServiceWorkSessions",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbWarehouse",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbWorkFlowActivityRecord",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbWorkFlowArchive",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbWorkflowAttachment",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbWorkFlowReviewLog",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbServicesRequest",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbTechnicalService",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbWorkFlow",
                schema: "ekb");

            migrationBuilder.DropTable(
                name: "EkbWorkFlowStep",
                schema: "ekb");
        }
    }
}
