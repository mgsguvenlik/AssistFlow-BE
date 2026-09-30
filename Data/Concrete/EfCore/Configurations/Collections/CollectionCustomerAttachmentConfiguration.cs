using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete;
using Model.Concrete.Collections;
namespace Data.Concrete.EfCore.Configurations.Collections;
public sealed class CollectionCustomerAttachmentConfiguration : IEntityTypeConfiguration<CollectionCustomerAttachment>
{
    public void Configure(EntityTypeBuilder<CollectionCustomerAttachment> b)
    {
        b.ToTable("CustomerAttachment", "collection"); b.HasKey(x => x.Id);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Property(x => x.SourcePath).HasMaxLength(500); b.HasIndex(x => x.SourcePath).IsUnique();
        b.Property(x => x.ArchiveHash).HasMaxLength(64); b.Property(x => x.ContentHash).HasMaxLength(64);
        b.Property(x => x.OriginalFileName).HasMaxLength(260); b.Property(x => x.StoredFileName).HasMaxLength(260);
        b.Property(x => x.ContentType).HasMaxLength(100); b.Property(x => x.Decision).HasMaxLength(1000);
        b.HasIndex(x => new { x.CustomerId, x.Id });
    }
}
