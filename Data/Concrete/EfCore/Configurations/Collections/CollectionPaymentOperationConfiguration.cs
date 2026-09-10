using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionPaymentOperationConfiguration : IEntityTypeConfiguration<CollectionPaymentOperation>
{
    public void Configure(EntityTypeBuilder<CollectionPaymentOperation> builder)
    {
        builder.ToTable("PaymentOperation", "collection", table =>
        {
            table.HasCheckConstraint("CK_PaymentOperation_Kind", "[Kind] IN (0,1,2)");
            table.HasCheckConstraint("CK_PaymentOperation_Hash", "DATALENGTH([PayloadHash]) = 32");
            table.HasCheckConstraint("CK_PaymentOperation_Before", "[BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1");
            table.HasCheckConstraint("CK_PaymentOperation_After", "[AfterJson] IS NULL OR ISJSON([AfterJson]) = 1");
            table.HasCheckConstraint("CK_PaymentOperation_Snapshots", "([Kind] = 0 AND [BeforeJson] IS NULL AND [AfterJson] IS NOT NULL) OR ([Kind] = 1 AND [BeforeJson] IS NOT NULL AND [AfterJson] IS NOT NULL) OR ([Kind] = 2 AND [BeforeJson] IS NOT NULL AND [AfterJson] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.PayloadHash).IsRequired().HasColumnType("varbinary(32)");
        builder.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.RequestId).IsUnique().HasDatabaseName("UX_PaymentOperation_RequestId");
        builder.HasIndex(x => new { x.PaymentId, x.CompletedDate, x.Id }).HasDatabaseName("IX_PaymentOperation_Payment_Date_Id");
    }
}
