using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "collection");

            migrationBuilder.CreateTable(
                name: "ContractStatus",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupStatus",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentFrequency",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IntervalMonths = table.Column<short>(type: "smallint", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentFrequency", x => x.Id);
                    table.CheckConstraint("CK_PaymentFrequency_IntervalMonths", "[IntervalMonths] IN (1,2,3,4,6,12,24,36)");
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethod",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethod", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentOperation",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOperation", x => x.Id);
                    table.CheckConstraint("CK_PaymentOperation_After", "[AfterJson] IS NULL OR ISJSON([AfterJson]) = 1");
                    table.CheckConstraint("CK_PaymentOperation_Before", "[BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1");
                    table.CheckConstraint("CK_PaymentOperation_Hash", "DATALENGTH([PayloadHash]) = 32");
                    table.CheckConstraint("CK_PaymentOperation_Kind", "[Kind] IN (0,1,2)");
                    table.CheckConstraint("CK_PaymentOperation_Snapshots", "([Kind] = 0 AND [BeforeJson] IS NULL AND [AfterJson] IS NOT NULL) OR ([Kind] = 1 AND [BeforeJson] IS NOT NULL AND [AfterJson] IS NOT NULL) OR ([Kind] = 2 AND [BeforeJson] IS NOT NULL AND [AfterJson] IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionStatus",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contract",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTypeId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GtsNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IvrNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubscriptionStatusId = table.Column<long>(type: "bigint", nullable: true),
                    ContractStatusId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentMethodId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contract", x => x.Id);
                    table.CheckConstraint("CK_Contract_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_Contract_ContractStatus_ContractStatusId",
                        column: x => x.ContractStatusId,
                        principalSchema: "collection",
                        principalTable: "ContractStatus",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contract_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contract_PaymentMethod_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalSchema: "collection",
                        principalTable: "PaymentMethod",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contract_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contract_SubscriptionStatus_SubscriptionStatusId",
                        column: x => x.SubscriptionStatusId,
                        principalSchema: "collection",
                        principalTable: "SubscriptionStatus",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractPeriodFollowUp",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<long>(type: "bigint", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    GroupStatusId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractPeriodFollowUp", x => x.Id);
                    table.CheckConstraint("CK_ContractPeriodFollowUp_Period", "DAY([Period]) = 1");
                    table.ForeignKey(
                        name: "FK_ContractPeriodFollowUp_Contract_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "collection",
                        principalTable: "Contract",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractPeriodFollowUp_GroupStatus_GroupStatusId",
                        column: x => x.GroupStatusId,
                        principalSchema: "collection",
                        principalTable: "GroupStatus",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractRatePeriod",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveToExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    BillingAnchor = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentFrequencyId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    BillingBehavior = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRatePeriod", x => x.Id);
                    table.CheckConstraint("CK_ContractRatePeriod_Amount", "[Amount] IS NULL OR [Amount] >= 0");
                    table.CheckConstraint("CK_ContractRatePeriod_Anchor", "[BillingAnchor] <= [EffectiveFrom]");
                    table.CheckConstraint("CK_ContractRatePeriod_Behavior", "[BillingBehavior] IN (0,1,2)");
                    table.CheckConstraint("CK_ContractRatePeriod_Billable", "[BillingBehavior] <> 0 OR ([Amount] IS NOT NULL AND [CurrencyTypeId] IS NOT NULL)");
                    table.CheckConstraint("CK_ContractRatePeriod_Dates", "[EffectiveToExclusive] IS NULL OR [EffectiveToExclusive] > [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_ContractRatePeriod_Contract_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "collection",
                        principalTable: "Contract",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractRatePeriod_CurrencyType_CurrencyTypeId",
                        column: x => x.CurrencyTypeId,
                        principalTable: "CurrencyType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractRatePeriod_PaymentFrequency_PaymentFrequencyId",
                        column: x => x.PaymentFrequencyId,
                        principalSchema: "collection",
                        principalTable: "PaymentFrequency",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<long>(type: "bigint", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyTypeId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUser = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.Id);
                    table.CheckConstraint("CK_Payment_Period", "DAY([Period]) = 1");
                    table.ForeignKey(
                        name: "FK_Payment_Contract_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "collection",
                        principalTable: "Contract",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payment_CurrencyType_CurrencyTypeId",
                        column: x => x.CurrencyTypeId,
                        principalTable: "CurrencyType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contract_ContractStatusId",
                schema: "collection",
                table: "Contract",
                column: "ContractStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Customer_Deleted_Id",
                schema: "collection",
                table: "Contract",
                columns: new[] { "CustomerId", "IsDeleted", "Id" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Contract_PaymentMethodId",
                schema: "collection",
                table: "Contract",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_ServiceType_Deleted_Id",
                schema: "collection",
                table: "Contract",
                columns: new[] { "ServiceTypeId", "IsDeleted", "Id" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Contract_SubscriptionStatusId",
                schema: "collection",
                table: "Contract",
                column: "SubscriptionStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractPeriodFollowUp_GroupStatusId",
                schema: "collection",
                table: "ContractPeriodFollowUp",
                column: "GroupStatusId");

            migrationBuilder.CreateIndex(
                name: "UX_ContractPeriodFollowUp_Contract_Period",
                schema: "collection",
                table: "ContractPeriodFollowUp",
                columns: new[] { "ContractId", "Period" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRatePeriod_CurrencyTypeId",
                schema: "collection",
                table: "ContractRatePeriod",
                column: "CurrencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRatePeriod_PaymentFrequencyId",
                schema: "collection",
                table: "ContractRatePeriod",
                column: "PaymentFrequencyId");

            migrationBuilder.CreateIndex(
                name: "UX_ContractRatePeriod_Open",
                schema: "collection",
                table: "ContractRatePeriod",
                column: "ContractId",
                unique: true,
                filter: "[EffectiveToExclusive] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_ContractRatePeriod_Start",
                schema: "collection",
                table: "ContractRatePeriod",
                columns: new[] { "ContractId", "EffectiveFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_ContractStatus_Code",
                schema: "collection",
                table: "ContractStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GroupStatus_Code",
                schema: "collection",
                table: "GroupStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Contract_Period_Currency_Id",
                schema: "collection",
                table: "Payment",
                columns: new[] { "ContractId", "Period", "CurrencyTypeId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_CurrencyTypeId",
                schema: "collection",
                table: "Payment",
                column: "CurrencyTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentFrequency_Code",
                schema: "collection",
                table: "PaymentFrequency",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentFrequency_IntervalMonths",
                schema: "collection",
                table: "PaymentFrequency",
                column: "IntervalMonths",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentMethod_Code",
                schema: "collection",
                table: "PaymentMethod",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOperation_Payment_Date_Id",
                schema: "collection",
                table: "PaymentOperation",
                columns: new[] { "PaymentId", "CompletedDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentOperation_RequestId",
                schema: "collection",
                table: "PaymentOperation",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SubscriptionStatus_Code",
                schema: "collection",
                table: "SubscriptionStatus",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractPeriodFollowUp",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "ContractRatePeriod",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "Payment",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "PaymentOperation",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "GroupStatus",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "PaymentFrequency",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "Contract",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "ContractStatus",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "PaymentMethod",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "SubscriptionStatus",
                schema: "collection");
        }
    }
}
