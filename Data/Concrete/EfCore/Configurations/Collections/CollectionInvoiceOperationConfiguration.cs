using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionInvoiceOperationConfiguration : IEntityTypeConfiguration<CollectionInvoiceOperation>
{
    public void Configure(EntityTypeBuilder<CollectionInvoiceOperation> b)
    {
        b.ToTable("InvoiceOperation", "collection", t =>
        {
            t.HasCheckConstraint("CK_InvoiceOperation_Kind", "[Kind] IN ('PaymentCreate','PaymentUpdate','PaymentDelete','CommentUpdate','InvoiceDelete')");
            t.HasCheckConstraint("CK_InvoiceOperation_Before", "[BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1");
            t.HasCheckConstraint("CK_InvoiceOperation_After", "[AfterJson] IS NULL OR ISJSON([AfterJson]) = 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(30).IsRequired();
        b.Property(x => x.PayloadHash).HasColumnType("binary(32)").IsRequired();
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.InvoiceId, x.CompletedDate, x.Id });
        b.HasIndex(x => new { x.LegacyInvoiceId, x.Kind });
        b.HasIndex(x => new { x.LegacyPaymentId, x.Kind });
    }
}
