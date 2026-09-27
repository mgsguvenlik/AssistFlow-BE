using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <summary>Indexes only; collection data and shared dbo tables are not changed.</summary>
    [DbContext(typeof(AppDataContext))]
    [Migration("20260927123000_AddCollectionTrackingIndexes")]
    public partial class AddCollectionTrackingIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ContractRatePeriod_Tracking",
                schema: "collection",
                table: "ContractRatePeriod",
                columns: new[] { "EffectiveFrom", "EffectiveToExclusive", "ContractId", "CurrencyTypeId" },
                filter: "[IsDeleted] = 0 AND [BillingBehavior] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Period_Contract_Currency_Id",
                schema: "collection",
                table: "Payment",
                columns: new[] { "Period", "ContractId", "CurrencyTypeId", "Id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ContractRatePeriod_Tracking", schema: "collection", table: "ContractRatePeriod");
            migrationBuilder.DropIndex(name: "IX_Payment_Period_Contract_Currency_Id", schema: "collection", table: "Payment");
        }
    }
}
