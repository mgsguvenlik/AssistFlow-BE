using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(AppDataContext))]
[Migration("20260927160000_AddCollectionGroupParent")]
public sealed class AddCollectionGroupParent : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "GroupParent", schema: "collection", columns: t => new
        {
            CustomerId = t.Column<long>(nullable: false),
            CustomerGroupId = t.Column<long>(nullable: false),
            LegacyCustomerId = t.Column<long>(nullable: false),
            AccountNo = t.Column<string>(maxLength: 200, nullable: true),
            IsCorporate = t.Column<bool>(nullable: false),
            SourceHash = t.Column<byte[]>(type: "binary(32)", nullable: false),
            CreatedDate = t.Column<DateTimeOffset>(nullable: false),
            CreatedUser = t.Column<long>(nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_GroupParent", x => x.CustomerId);
            t.ForeignKey("FK_GroupParent_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", principalSchema: "dbo");
            t.ForeignKey("FK_GroupParent_CustomerGroups_CustomerGroupId", x => x.CustomerGroupId, "CustomerGroups", "Id", principalSchema: "dbo");
        });
        m.CreateIndex("IX_GroupParent_LegacyCustomerId", "GroupParent", "LegacyCustomerId", "collection", unique: true);
        m.CreateIndex("IX_GroupParent_CustomerGroupId", "GroupParent", "CustomerGroupId", "collection", unique: true);
        m.CreateIndex("IX_GroupParent_AccountNo", "GroupParent", "AccountNo", "collection");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("GroupParent", "collection");
}
