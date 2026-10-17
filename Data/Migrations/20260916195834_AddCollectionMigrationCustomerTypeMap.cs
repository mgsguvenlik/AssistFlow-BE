using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionMigrationCustomerTypeMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MigrationReferenceMap_Target",
                schema: "collection",
                table: "MigrationReferenceMap");

            migrationBuilder.AddColumn<long>(
                name: "TargetCustomerTypeId",
                schema: "collection",
                table: "MigrationReferenceMap",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReferenceMap_TargetCustomerTypeId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetCustomerTypeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MigrationReferenceMap_Kind",
                schema: "collection",
                table: "MigrationReferenceMap",
                sql: "[Status] <> 1 OR ([ReferenceKind] = N'Customer' AND [TargetCustomerId] IS NOT NULL) OR ([ReferenceKind] = N'CustomerType' AND [TargetCustomerTypeId] IS NOT NULL) OR ([ReferenceKind] = N'ServiceType' AND [TargetServiceTypeId] IS NOT NULL) OR ([ReferenceKind] = N'CurrencyType' AND [TargetCurrencyTypeId] IS NOT NULL) OR ([ReferenceKind] = N'PaymentFrequency' AND [TargetPaymentFrequencyId] IS NOT NULL) OR ([ReferenceKind] = N'PaymentMethod' AND [TargetPaymentMethodId] IS NOT NULL) OR ([ReferenceKind] = N'SubscriptionStatus' AND [TargetSubscriptionStatusId] IS NOT NULL) OR ([ReferenceKind] = N'ContractStatus' AND [TargetContractStatusId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MigrationReferenceMap_Target",
                schema: "collection",
                table: "MigrationReferenceMap",
                sql: "[Status] <> 1 OR (CASE WHEN [TargetCustomerId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCustomerTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetServiceTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCurrencyTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentFrequencyId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentMethodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetSubscriptionStatusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetContractStatusId] IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationReferenceMap_CustomerType_TargetCustomerTypeId",
                schema: "collection",
                table: "MigrationReferenceMap",
                column: "TargetCustomerTypeId",
                principalTable: "CustomerType",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MigrationReferenceMap_CustomerType_TargetCustomerTypeId",
                schema: "collection",
                table: "MigrationReferenceMap");

            migrationBuilder.DropIndex(
                name: "IX_MigrationReferenceMap_TargetCustomerTypeId",
                schema: "collection",
                table: "MigrationReferenceMap");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MigrationReferenceMap_Kind",
                schema: "collection",
                table: "MigrationReferenceMap");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MigrationReferenceMap_Target",
                schema: "collection",
                table: "MigrationReferenceMap");

            migrationBuilder.DropColumn(
                name: "TargetCustomerTypeId",
                schema: "collection",
                table: "MigrationReferenceMap");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MigrationReferenceMap_Target",
                schema: "collection",
                table: "MigrationReferenceMap",
                sql: "[Status] <> 1 OR (CASE WHEN [TargetCustomerId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetServiceTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCurrencyTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentFrequencyId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentMethodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetSubscriptionStatusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetContractStatusId] IS NULL THEN 0 ELSE 1 END) = 1");
        }
    }
}
