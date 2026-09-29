using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionInvoiceConfiguration : IEntityTypeConfiguration<CollectionInvoice>
{
    public void Configure(EntityTypeBuilder<CollectionInvoice> b)
    {
        b.ToTable("Invoice", "collection", t => t.HasCheckConstraint("CK_Invoice_Type", "[Type] IN ('B', 'K')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(1).IsRequired();
        b.Property(x => x.Number).HasMaxLength(50);
        b.Property(x => x.ProjectCode).HasMaxLength(100);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.LegacyCreatedBy).HasMaxLength(100);
        b.Property(x => x.LegacyModifiedBy).HasMaxLength(100);
        b.Property(x => x.SourceHash).HasColumnType("binary(32)");
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.CurrencyType).WithMany().HasForeignKey(x => x.CurrencyTypeId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.Type, x.Date, x.Id });
        b.HasIndex(x => new { x.CustomerId, x.Date, x.Id });
        b.HasIndex(x => x.LegacyInvoiceFollowId).IsUnique().HasFilter("[LegacyInvoiceFollowId] IS NOT NULL");
    }
}

public sealed class CollectionInvoicePaymentConfiguration : IEntityTypeConfiguration<CollectionInvoicePayment>
{
    public void Configure(EntityTypeBuilder<CollectionInvoicePayment> b)
    {
        b.ToTable("InvoicePayment", "collection");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.LegacyCreatedBy).HasMaxLength(100);
        b.Property(x => x.LegacyModifiedBy).HasMaxLength(100);
        b.Property(x => x.SourceHash).HasColumnType("binary(32)");
        b.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.InvoiceId, x.Date, x.Id });
        b.HasIndex(x => x.LegacyInvoiceFollowPaymentId).IsUnique().HasFilter("[LegacyInvoiceFollowPaymentId] IS NOT NULL");
    }
}
