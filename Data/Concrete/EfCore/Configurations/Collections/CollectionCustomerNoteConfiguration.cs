using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionCustomerNoteConfiguration : IEntityTypeConfiguration<CollectionCustomerNote>
{
    public void Configure(EntityTypeBuilder<CollectionCustomerNote> b)
    {
        b.ToTable("CustomerNote", "collection");
        b.HasKey(x => x.Id);
        b.Property(x => x.Text).HasMaxLength(10000).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.LegacyCreatedBy).HasMaxLength(200);
        b.Property(x => x.LegacyModifiedBy).HasMaxLength(200);
        b.Property(x => x.SourceHash).HasColumnType("binary(32)");
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.CustomerId, x.IsDeleted, x.CreatedDate, x.Id });
        b.HasIndex(x => x.LegacyCommentId).IsUnique().HasFilter("[LegacyCommentId] IS NOT NULL");
    }
}

