using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionInvoiceAccountConfiguration : IEntityTypeConfiguration<CollectionInvoiceAccount>
{
    public void Configure(EntityTypeBuilder<CollectionInvoiceAccount> b)
    {
        b.ToTable("InvoiceAccount", "collection"); b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Issue).HasMaxLength(500);
        b.Property(x => x.SourceHash).HasMaxLength(64);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
    }
}
public sealed class CollectionInvoiceLoadConfiguration : IEntityTypeConfiguration<CollectionInvoiceLoad>
{
    public void Configure(EntityTypeBuilder<CollectionInvoiceLoad> b)
    {
        b.ToTable("InvoiceLoad", "collection", t => t.HasCheckConstraint("CK_InvoiceLoad_Type", "[Type] IN ('B','K')")); b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(1); b.Property(x => x.FileHash).HasMaxLength(64);
        b.Property(x => x.FileName).HasMaxLength(260); b.Property(x => x.StoredFileName).HasMaxLength(260);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Type, x.FileHash }).IsUnique();
        b.HasIndex(x => new { x.CreatedDate, x.Id });
    }
}
public sealed class CollectionInvoiceLoadRowConfiguration : IEntityTypeConfiguration<CollectionInvoiceLoadRow>
{
    public void Configure(EntityTypeBuilder<CollectionInvoiceLoadRow> b)
    {
        b.ToTable("InvoiceLoadRow", "collection", t => t.HasCheckConstraint("CK_InvoiceLoadRow_Status", "[Status] IN ('Ready','Error','Duplicate','Imported')")); b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.Issue).HasMaxLength(4000);
        b.Property(x => x.ImportedKey).HasMaxLength(64);
        b.HasOne(x => x.Load).WithMany(x => x.Rows).HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.LoadId, x.RowNumber }).IsUnique();
        b.HasIndex(x => new { x.LoadId, x.Status, x.RowNumber });
        b.HasIndex(x => x.ImportedKey).IsUnique().HasFilter("[ImportedKey] IS NOT NULL");
    }
}
