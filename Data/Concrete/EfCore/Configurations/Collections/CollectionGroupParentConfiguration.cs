using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionGroupParentConfiguration : IEntityTypeConfiguration<CollectionGroupParent>
{
    public void Configure(EntityTypeBuilder<CollectionGroupParent> b)
    {
        b.ToTable("GroupParent", "collection");
        b.HasKey(x => x.CustomerId);
        b.Property(x => x.CustomerId).ValueGeneratedNever();
        b.Property(x => x.AccountNo).HasMaxLength(200);
        b.Property(x => x.SourceHash).HasColumnType("binary(32)").IsRequired();
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CustomerGroup>().WithMany().HasForeignKey(x => x.CustomerGroupId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.LegacyCustomerId).IsUnique();
        b.HasIndex(x => x.CustomerGroupId).IsUnique();
        // Cari kod tekil varsayılmaz; okuma servisinde belirsizlik reddedilir.
        b.HasIndex(x => x.AccountNo);
    }
}
