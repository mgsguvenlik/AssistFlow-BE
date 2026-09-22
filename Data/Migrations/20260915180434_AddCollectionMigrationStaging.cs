using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionMigrationStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationBatch",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ManifestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizationVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUser = table.Column<long>(type: "bigint", nullable: false),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationBatch", x => x.Id);
                    table.CheckConstraint("CK_MigrationBatch_Hash", "DATALENGTH([ManifestHash]) = 32");
                    table.CheckConstraint("CK_MigrationBatch_Status", "[Status] BETWEEN 0 AND 6");
                });

            migrationBuilder.CreateTable(
                name: "MigrationMap",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetContractId = table.Column<long>(type: "bigint", nullable: true),
                    TargetRatePeriodId = table.Column<long>(type: "bigint", nullable: true),
                    TargetPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    AppliedPayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    FirstBatchId = table.Column<long>(type: "bigint", nullable: false),
                    LastBatchId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationMap", x => x.Id);
                    table.CheckConstraint("CK_MigrationMap_Hash", "DATALENGTH([AppliedPayloadHash]) = 32");
                    table.CheckConstraint("CK_MigrationMap_Target", "(CASE WHEN [TargetContractId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetRatePeriodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_MigrationMap_ContractRatePeriod_TargetRatePeriodId",
                        column: x => x.TargetRatePeriodId,
                        principalSchema: "collection",
                        principalTable: "ContractRatePeriod",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationMap_Contract_TargetContractId",
                        column: x => x.TargetContractId,
                        principalSchema: "collection",
                        principalTable: "Contract",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationMap_MigrationBatch_FirstBatchId",
                        column: x => x.FirstBatchId,
                        principalSchema: "collection",
                        principalTable: "MigrationBatch",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationMap_MigrationBatch_LastBatchId",
                        column: x => x.LastBatchId,
                        principalSchema: "collection",
                        principalTable: "MigrationBatch",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationMap_Payment_TargetPaymentId",
                        column: x => x.TargetPaymentId,
                        principalSchema: "collection",
                        principalTable: "Payment",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MigrationReferenceMap",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    TargetServiceTypeId = table.Column<long>(type: "bigint", nullable: true),
                    TargetCurrencyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    TargetPaymentFrequencyId = table.Column<long>(type: "bigint", nullable: true),
                    TargetPaymentMethodId = table.Column<long>(type: "bigint", nullable: true),
                    TargetSubscriptionStatusId = table.Column<long>(type: "bigint", nullable: true),
                    TargetContractStatusId = table.Column<long>(type: "bigint", nullable: true),
                    MatchMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    DecidedUser = table.Column<long>(type: "bigint", nullable: true),
                    DecidedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationReferenceMap", x => x.Id);
                    table.CheckConstraint("CK_MigrationReferenceMap_Status", "[Status] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_MigrationReferenceMap_Target", "[Status] <> 1 OR (CASE WHEN [TargetCustomerId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetServiceTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCurrencyTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentFrequencyId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentMethodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetSubscriptionStatusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetContractStatusId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_ContractStatus_TargetContractStatusId",
                        column: x => x.TargetContractStatusId,
                        principalSchema: "collection",
                        principalTable: "ContractStatus",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_CurrencyType_TargetCurrencyTypeId",
                        column: x => x.TargetCurrencyTypeId,
                        principalTable: "CurrencyType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_Customers_TargetCustomerId",
                        column: x => x.TargetCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_MigrationBatch_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "collection",
                        principalTable: "MigrationBatch",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_PaymentFrequency_TargetPaymentFrequencyId",
                        column: x => x.TargetPaymentFrequencyId,
                        principalSchema: "collection",
                        principalTable: "PaymentFrequency",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_PaymentMethod_TargetPaymentMethodId",
                        column: x => x.TargetPaymentMethodId,
                        principalSchema: "collection",
                        principalTable: "PaymentMethod",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_ServiceType_TargetServiceTypeId",
                        column: x => x.TargetServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationReferenceMap_SubscriptionStatus_TargetSubscriptionStatusId",
                        column: x => x.TargetSubscriptionStatusId,
                        principalSchema: "collection",
                        principalTable: "SubscriptionStatus",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MigrationSourceRow",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    EntityCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceParentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    StagedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationSourceRow", x => x.Id);
                    table.CheckConstraint("CK_MigrationSourceRow_Hash", "DATALENGTH([PayloadHash]) = 32");
                    table.CheckConstraint("CK_MigrationSourceRow_Payload", "ISJSON([Payload]) = 1");
                    table.ForeignKey(
                        name: "FK_MigrationSourceRow_MigrationBatch_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "collection",
                        principalTable: "MigrationBatch",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MigrationContractStage",
                schema: "collection",
                columns: table => new
                {
                    SourceRowId = table.Column<long>(type: "bigint", nullable: false),
                    SourceCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubscriberNoRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubscriberNoNormalized = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceServiceTypeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceContractStatusId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceSubscriptionStatusId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourcePaymentMethodId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartingMonth = table.Column<short>(type: "smallint", nullable: true),
                    StartingYear = table.Column<short>(type: "smallint", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GtsNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IvrNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AttachmentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    TargetServiceTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationContractStage", x => x.SourceRowId);
                    table.CheckConstraint("CK_MigrationContractStage_Status", "[Status] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_MigrationContractStage_Customers_TargetCustomerId",
                        column: x => x.TargetCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationContractStage_MigrationSourceRow_SourceRowId",
                        column: x => x.SourceRowId,
                        principalSchema: "collection",
                        principalTable: "MigrationSourceRow",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationContractStage_ServiceType_TargetServiceTypeId",
                        column: x => x.TargetServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MigrationIssue",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceRowId = table.Column<long>(type: "bigint", nullable: false),
                    IssueCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Severity = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RuleVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedUser = table.Column<long>(type: "bigint", nullable: true),
                    ResolvedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationIssue", x => x.Id);
                    table.CheckConstraint("CK_MigrationIssue_Severity", "[Severity] IN (0,1)");
                    table.CheckConstraint("CK_MigrationIssue_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_MigrationIssue_MigrationSourceRow_SourceRowId",
                        column: x => x.SourceRowId,
                        principalSchema: "collection",
                        principalTable: "MigrationSourceRow",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MigrationRatePeriodStage",
                schema: "collection",
                columns: table => new
                {
                    SourceRowId = table.Column<long>(type: "bigint", nullable: false),
                    SourceContractId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveToExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SourceCurrencyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourcePaymentTypeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetCurrencyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    TargetPaymentFrequencyId = table.Column<long>(type: "bigint", nullable: true),
                    BillingBehavior = table.Column<byte>(type: "tinyint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationRatePeriodStage", x => x.SourceRowId);
                    table.CheckConstraint("CK_MigrationRateStage_Behavior", "[BillingBehavior] IS NULL OR [BillingBehavior] IN (0,1,2)");
                    table.CheckConstraint("CK_MigrationRateStage_Status", "[Status] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_MigrationRatePeriodStage_CurrencyType_TargetCurrencyTypeId",
                        column: x => x.TargetCurrencyTypeId,
                        principalTable: "CurrencyType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationRatePeriodStage_MigrationSourceRow_SourceRowId",
                        column: x => x.SourceRowId,
                        principalSchema: "collection",
                        principalTable: "MigrationSourceRow",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationRatePeriodStage_PaymentFrequency_TargetPaymentFrequencyId",
                        column: x => x.TargetPaymentFrequencyId,
                        principalSchema: "collection",
                        principalTable: "PaymentFrequency",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UX_MigrationBatch_Source_Snapshot",
                schema: "collection",
                table: "MigrationBatch",
                columns: new[] { "SourceSystem", "SnapshotKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationContractStage_Status_Row",
                schema: "collection",
                table: "MigrationContractStage",
                columns: new[] { "Status", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationContractStage_Subscriber",
                schema: "collection",
                table: "MigrationContractStage",
                column: "SubscriberNoNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationContractStage_TargetCustomerId",
                schema: "collection",
                table: "MigrationContractStage",
                column: "TargetCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationContractStage_TargetServiceTypeId",
                schema: "collection",
                table: "MigrationContractStage",
                column: "TargetServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationIssue_Status_Code_Row_Id",
                schema: "collection",
                table: "MigrationIssue",
                columns: new[] { "Status", "IssueCode", "SourceRowId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_MigrationIssue_Open_Row_Code",
                schema: "collection",
                table: "MigrationIssue",
                columns: new[] { "SourceRowId", "IssueCode" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationMap_FirstBatchId",
                schema: "collection",
                table: "MigrationMap",
                column: "FirstBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationMap_LastBatchId",
                schema: "collection",
                table: "MigrationMap",
                column: "LastBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_MigrationMap_Source_Entity_Id",
                schema: "collection",
                table: "MigrationMap",
                columns: new[] { "SourceSystem", "EntityCode", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MigrationMap_TargetContract",
                schema: "collection",
                table: "MigrationMap",
                column: "TargetContractId",
                unique: true,
                filter: "[TargetContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MigrationMap_TargetPayment",
                schema: "collection",
                table: "MigrationMap",
                column: "TargetPaymentId",
                unique: true,
                filter: "[TargetPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MigrationMap_TargetRate",
                schema: "collection",
                table: "MigrationMap",
                column: "TargetRatePeriodId",
                unique: true,
                filter: "[TargetRatePeriodId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRatePeriodStage_TargetCurrencyTypeId",
                schema: "collection",
                table: "MigrationRatePeriodStage",
                column: "TargetCurrencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRatePeriodStage_TargetPaymentFrequencyId",
                schema: "collection",
                table: "MigrationRatePeriodStage",
                column: "TargetPaymentFrequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRateStage_Contract_Date_Row",
                schema: "collection",
                table: "MigrationRatePeriodStage",
                columns: new[] { "SourceContractId", "EffectiveFrom", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRateStage_Status_Row",
                schema: "collection",
                table: "MigrationRatePeriodStage",
                columns: new[] { "Status", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_Status_Kind_Id",
                schema: "collection",
                table: "MigrationReferenceMap",
                columns: new[] { "Status", "ReferenceKind", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetContractStatusId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetContractStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetCurrencyTypeId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetCurrencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetCustomerId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetPaymentFrequencyId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetPaymentFrequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetPaymentMethodId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetPaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetServiceTypeId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetSubscriptionStatusId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetSubscriptionStatusId");

            migrationBuilder.CreateIndex(
                name: "UX_MigrationReferenceMap_Batch_Kind_Source",
                schema: "collection",
                table: "MigrationReferenceMap",
                columns: new[] { "BatchId", "ReferenceKind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationSourceRow_Batch_Entity_Id",
                schema: "collection",
                table: "MigrationSourceRow",
                columns: new[] { "BatchId", "EntityCode", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationSourceRow_Batch_Parent_Id",
                schema: "collection",
                table: "MigrationSourceRow",
                columns: new[] { "BatchId", "EntityCode", "SourceParentId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_MigrationSourceRow_Batch_Entity_Source",
                schema: "collection",
                table: "MigrationSourceRow",
                columns: new[] { "BatchId", "EntityCode", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationContractStage",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "MigrationIssue",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "MigrationMap",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "MigrationRatePeriodStage",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "MigrationReferenceMap",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "MigrationSourceRow",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "MigrationBatch",
                schema: "collection");
        }
    }
}
