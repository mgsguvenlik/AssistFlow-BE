using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "UpdatedUser",
                table: "Users",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "CustomerId1",
                table: "ProgressApprovers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedUser",
                table: "Product",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedUser",
                table: "Customers",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "PriceGroupId",
                table: "Customers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PriceGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceGroups_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkFlowStatuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServicesRequests",
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
                    IsSended = table.Column<bool>(type: "bit", nullable: false),
                    WorkFlowStatusId = table.Column<long>(type: "bigint", nullable: true),
                    SendedStatusId = table.Column<long>(type: "bigint", nullable: true),
                    IsReview = table.Column<bool>(type: "bit", nullable: false),
                    IsMailSended = table.Column<bool>(type: "bit", nullable: false),
                    CustomerApproverId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTypeId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicesRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServicesRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServicesRequests_ProgressApprovers_CustomerApproverId",
                        column: x => x.CustomerApproverId,
                        principalTable: "ProgressApprovers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServicesRequests_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServicesRequests_WorkFlowStatuses_WorkFlowStatusId",
                        column: x => x.WorkFlowStatusId,
                        principalTable: "WorkFlowStatuses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkFlows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestTitle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RequestNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StatuId = table.Column<long>(type: "bigint", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    IsComplated = table.Column<bool>(type: "bit", nullable: false),
                    ReconciliationStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkFlows_WorkFlowStatuses_StatuId",
                        column: x => x.StatuId,
                        principalTable: "WorkFlowStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServicesRequestProducts",
                columns: table => new
                {
                    ServicesRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicesRequestProducts", x => new { x.ServicesRequestId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ServicesRequestProducts_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServicesRequestProducts_ServicesRequests_ServicesRequestId",
                        column: x => x.ServicesRequestId,
                        principalTable: "ServicesRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressApprovers_CustomerId1",
                table: "ProgressApprovers",
                column: "CustomerId1");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PriceGroupId",
                table: "Customers",
                column: "PriceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceGroups_ProductId",
                table: "PriceGroups",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequestProducts_ProductId",
                table: "ServicesRequestProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequests_CustomerApproverId",
                table: "ServicesRequests",
                column: "CustomerApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequests_CustomerId",
                table: "ServicesRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequests_RequestNo",
                table: "ServicesRequests",
                column: "RequestNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequests_ServiceTypeId",
                table: "ServicesRequests",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicesRequests_WorkFlowStatusId",
                table: "ServicesRequests",
                column: "WorkFlowStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlows_RequestNo",
                table: "WorkFlows",
                column: "RequestNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkFlows_StatuId",
                table: "WorkFlows",
                column: "StatuId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_PriceGroups_PriceGroupId",
                table: "Customers",
                column: "PriceGroupId",
                principalTable: "PriceGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressApprovers_Customers_CustomerId1",
                table: "ProgressApprovers",
                column: "CustomerId1",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_PriceGroups_PriceGroupId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgressApprovers_Customers_CustomerId1",
                table: "ProgressApprovers");

            migrationBuilder.DropTable(
                name: "PriceGroups");

            migrationBuilder.DropTable(
                name: "ServicesRequestProducts");

            migrationBuilder.DropTable(
                name: "WorkFlows");

            migrationBuilder.DropTable(
                name: "ServicesRequests");

            migrationBuilder.DropTable(
                name: "WorkFlowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_ProgressApprovers_CustomerId1",
                table: "ProgressApprovers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PriceGroupId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerId1",
                table: "ProgressApprovers");

            migrationBuilder.DropColumn(
                name: "PriceGroupId",
                table: "Customers");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedUser",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedUser",
                table: "Product",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedUser",
                table: "Customers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
