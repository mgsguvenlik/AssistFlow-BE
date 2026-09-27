using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(AppDataContext))]
[Migration("20260927190000_AddCollectionGroupFollowUpSource")]
public sealed class AddCollectionGroupFollowUpSource : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<long>("LegacyFollowGroupStatusId", "ContractPeriodFollowUp", schema: "collection", nullable: true);
        m.AddColumn<byte[]>("SourceHash", "ContractPeriodFollowUp", schema: "collection", type: "binary(32)", nullable: true);
        m.CreateIndex("IX_ContractPeriodFollowUp_LegacyFollowGroupStatusId", "ContractPeriodFollowUp", "LegacyFollowGroupStatusId",
            "collection", unique: true, filter: "[LegacyFollowGroupStatusId] IS NOT NULL");
        m.CreateIndex("IX_ContractPeriodFollowUp_IsDeleted_Period_Id", "ContractPeriodFollowUp",
            new[] { "IsDeleted", "Period", "Id" }, "collection");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropIndex("IX_ContractPeriodFollowUp_LegacyFollowGroupStatusId", "ContractPeriodFollowUp", "collection");
        m.DropIndex("IX_ContractPeriodFollowUp_IsDeleted_Period_Id", "ContractPeriodFollowUp", "collection");
        m.DropColumn("LegacyFollowGroupStatusId", "ContractPeriodFollowUp", "collection");
        m.DropColumn("SourceHash", "ContractPeriodFollowUp", "collection");
    }
}

