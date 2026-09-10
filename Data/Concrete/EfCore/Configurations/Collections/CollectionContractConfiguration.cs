using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

/// <summary>Draft mapping; deliberately not applied to the application's active EF model yet.</summary>
public sealed class CollectionContractConfiguration : IEntityTypeConfiguration<CollectionContract>
{
    public void Configure(EntityTypeBuilder<CollectionContract> builder)
    {
        builder.ToTable("Contract", "collection", table =>
        {
            table.HasCheckConstraint("CK_Contract_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.HasOne(x => x.SubscriptionStatus).WithMany().HasForeignKey(x => x.SubscriptionStatusId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ContractStatus).WithMany().HasForeignKey(x => x.ContractStatusId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.GtsNo).HasMaxLength(50);
        builder.Property(x => x.IvrNo).HasMaxLength(50);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ServiceType).WithMany().HasForeignKey(x => x.ServiceTypeId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.CustomerId, x.IsDeleted, x.Id })
            .IsDescending(false, false, true).HasDatabaseName("IX_Contract_Customer_Deleted_Id");
        builder.HasIndex(x => new { x.ServiceTypeId, x.IsDeleted, x.Id })
            .IsDescending(false, false, true).HasDatabaseName("IX_Contract_ServiceType_Deleted_Id");
    }
}
