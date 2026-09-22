using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionContractAttachmentConfiguration : IEntityTypeConfiguration<CollectionContractAttachment>
{
    public void Configure(EntityTypeBuilder<CollectionContractAttachment> builder)
    {
        builder.ToTable("ContractAttachment", "collection", table =>
            table.HasCheckConstraint("CK_ContractAttachment_Size", "[SizeBytes] > 0 AND [SizeBytes] <= 20971520"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.StoredFileName).IsUnique().HasDatabaseName("UX_ContractAttachment_StoredFileName");
        builder.HasIndex(x => new { x.ContractId, x.IsDeleted, x.Id }).IsDescending(false, false, true)
            .HasDatabaseName("IX_ContractAttachment_Contract_Deleted_Id");
    }
}
