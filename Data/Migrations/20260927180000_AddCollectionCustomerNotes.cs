using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(AppDataContext))]
[Migration("20260927180000_AddCollectionCustomerNotes")]
public sealed class AddCollectionCustomerNotes : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "CustomerNote", schema: "collection", columns: t => new
        {
            Id = t.Column<long>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            CustomerId = t.Column<long>(nullable: false),
            Text = t.Column<string>(maxLength: 10000, nullable: false),
            RowVersion = t.Column<byte[]>(rowVersion: true, nullable: false),
            CreatedDate = t.Column<DateTimeOffset>(nullable: false),
            UpdatedDate = t.Column<DateTimeOffset>(nullable: true),
            CreatedUser = t.Column<long>(nullable: false),
            UpdatedUser = t.Column<long>(nullable: true),
            IsDeleted = t.Column<bool>(nullable: false),
            LegacyCommentId = t.Column<long>(nullable: true),
            LegacyCustomerId = t.Column<long>(nullable: true),
            LegacyCreatedBy = t.Column<string>(maxLength: 200, nullable: true),
            LegacyModifiedBy = t.Column<string>(maxLength: 200, nullable: true),
            LegacyCreatedOn = t.Column<DateTime>(nullable: true),
            LegacyModifiedOn = t.Column<DateTime>(nullable: true),
            SourceHash = t.Column<byte[]>(type: "binary(32)", nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_CustomerNote", x => x.Id);
            t.ForeignKey("FK_CustomerNote_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", principalSchema: "dbo");
        });
        m.CreateIndex("IX_CustomerNote_CustomerId_IsDeleted_CreatedDate_Id", "CustomerNote",
            new[] { "CustomerId", "IsDeleted", "CreatedDate", "Id" }, "collection");
        m.CreateIndex("IX_CustomerNote_LegacyCommentId", "CustomerNote", "LegacyCommentId", "collection",
            unique: true, filter: "[LegacyCommentId] IS NOT NULL");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("CustomerNote", "collection");
}

