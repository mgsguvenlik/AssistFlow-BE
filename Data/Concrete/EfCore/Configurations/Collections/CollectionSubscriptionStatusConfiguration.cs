using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionSubscriptionStatusConfiguration : IEntityTypeConfiguration<CollectionSubscriptionStatus>
{
    public void Configure(EntityTypeBuilder<CollectionSubscriptionStatus> builder)
    {
        builder.ToTable("SubscriptionStatus", "collection");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_SubscriptionStatus_Code");
    }
}
