using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionGroupStatusConfiguration : IEntityTypeConfiguration<CollectionGroupStatus>
{
    public void Configure(EntityTypeBuilder<CollectionGroupStatus> builder)
    {
        builder.ToTable("GroupStatus", "collection");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_GroupStatus_Code");
    }
}
