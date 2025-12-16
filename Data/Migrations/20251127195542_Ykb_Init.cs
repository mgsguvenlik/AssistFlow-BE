using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class Ykb_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ykb");

            migrationBuilder.CreateTable(
                name: "YkbCustomerForm",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    YkbServiceTrackNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_YkbCustomerForm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbCustomerForm_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YkbCustomerForm_ProgressApprovers_CustomerApproverId",
                        column: x => x.CustomerApproverId,
                        principalTable: "ProgressApprovers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "YkbFinalApproval",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedBy = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YkbFinalApproval", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YkbPricing",
                schema: "ykb",
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
                    table.PrimaryKey("PK_YkbPricing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YkbServicesRequestProduct",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_YkbServicesRequestProduct", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbServicesRequestProduct_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YkbServicesRequestProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YkbTechnicalService",
                schema: "ykb",
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
                    table.PrimaryKey("PK_YkbTechnicalService", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbTechnicalService_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "YkbWarehouse",
                schema: "ykb",
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
                    table.PrimaryKey("PK_YkbWarehouse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YkbWorkFlowActivityRecord",
                schema: "ykb",
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
                    table.PrimaryKey("PK_YkbWorkFlowActivityRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbWorkFlowActivityRecord_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YkbWorkFlowActivityRecord_WorkFlows_WorkFlowId",
                        column: x => x.WorkFlowId,
                        principalTable: "WorkFlows",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "YkbWorkFlowArchive",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchiveReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbServicesRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbServicesRequestProductsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproverTechnicianJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerApproverJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbWorkFlowJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbWorkFlowReviewLogsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbTechnicalServiceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbTechnicalServiceImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbTechnicalServiceFormImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbWarehouseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbPricingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YkbFinalApprovalJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YkbWorkFlowArchive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YkbWorkFlowReviewLog",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YkbWorkFlowId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_YkbWorkFlowReviewLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YkbWorkFlowStep",
                schema: "ykb",
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
                    table.PrimaryKey("PK_YkbWorkFlowStep", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YkbTechnicalServiceFormImage",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YkbTechnicalServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YkbTechnicalServiceFormImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbTechnicalServiceFormImage_YkbTechnicalService_YkbTechnicalServiceId",
                        column: x => x.YkbTechnicalServiceId,
                        principalSchema: "ykb",
                        principalTable: "YkbTechnicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YkbTechnicalServiceImage",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YkbTechnicalServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YkbTechnicalServiceImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbTechnicalServiceImage_YkbTechnicalService_YkbTechnicalServiceId",
                        column: x => x.YkbTechnicalServiceId,
                        principalSchema: "ykb",
                        principalTable: "YkbTechnicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YkbServicesRequest",
                schema: "ykb",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OracleNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ServicesDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlannedCompletionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ServicesCostStatus = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsProductRequirement = table.Column<bool>(type: "bit", nullable: false),
                    YkbWorkFlowStepId = table.Column<long>(type: "bigint", nullable: true),
                    WorkFlowStepId = table.Column<long>(type: "bigint", nullable: true),
                    IsMailSended = table.Column<bool>(type: "bit", nullable: false),
                    CustomerApproverId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTypeId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_YkbServicesRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbServicesRequest_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YkbServicesRequest_ProgressApprovers_CustomerApproverId",
                        column: x => x.CustomerApproverId,
                        principalTable: "ProgressApprovers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YkbServicesRequest_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YkbServicesRequest_YkbWorkFlowStep_YkbWorkFlowStepId",
                        column: x => x.YkbWorkFlowStepId,
                        principalSchema: "ykb",
                        principalTable: "YkbWorkFlowStep",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "YkbWorkFlow",
                schema: "ykb",
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
                    table.PrimaryKey("PK_YkbWorkFlow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YkbWorkFlow_Users_ApproverTechnicianId",
                        column: x => x.ApproverTechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YkbWorkFlow_YkbWorkFlowStep_CurrentStepId",
                        column: x => x.CurrentStepId,
                        principalSchema: "ykb",
                        principalTable: "YkbWorkFlowStep",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_YkbCustomerForm_CustomerApproverId",
                schema: "ykb",
                table: "YkbCustomerForm",
                column: "CustomerApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbCustomerForm_CustomerId",
                schema: "ykb",
                table: "YkbCustomerForm",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbCustomerForm_RequestNo",
                schema: "ykb",
                table: "YkbCustomerForm",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequest_CustomerApproverId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "CustomerApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequest_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequest_RequestNo",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequest_ServiceTypeId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequest_YkbWorkFlowStepId",
                schema: "ykb",
                table: "YkbServicesRequest",
                column: "YkbWorkFlowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequestProduct_CustomerId",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbServicesRequestProduct_ProductId",
                schema: "ykb",
                table: "YkbServicesRequestProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbTechnicalService_ServiceTypeId",
                schema: "ykb",
                table: "YkbTechnicalService",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbTechnicalServiceFormImage_YkbTechnicalServiceId",
                schema: "ykb",
                table: "YkbTechnicalServiceFormImage",
                column: "YkbTechnicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbTechnicalServiceImage_YkbTechnicalServiceId",
                schema: "ykb",
                table: "YkbTechnicalServiceImage",
                column: "YkbTechnicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbWorkFlow_ApproverTechnicianId",
                schema: "ykb",
                table: "YkbWorkFlow",
                column: "ApproverTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbWorkFlow_CurrentStepId",
                schema: "ykb",
                table: "YkbWorkFlow",
                column: "CurrentStepId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbWorkFlow_RequestNo",
                schema: "ykb",
                table: "YkbWorkFlow",
                column: "RequestNo");

            migrationBuilder.CreateIndex(
                name: "IX_YkbWorkFlowActivityRecord_CustomerId",
                schema: "ykb",
                table: "YkbWorkFlowActivityRecord",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_YkbWorkFlowActivityRecord_WorkFlowId",
                schema: "ykb",
                table: "YkbWorkFlowActivityRecord",
                column: "WorkFlowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YkbCustomerForm",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbFinalApproval",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbPricing",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbServicesRequest",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbServicesRequestProduct",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbTechnicalServiceFormImage",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbTechnicalServiceImage",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbWarehouse",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbWorkFlow",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbWorkFlowActivityRecord",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbWorkFlowArchive",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbWorkFlowReviewLog",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbTechnicalService",
                schema: "ykb");

            migrationBuilder.DropTable(
                name: "YkbWorkFlowStep",
                schema: "ykb");
        }
    }
}
