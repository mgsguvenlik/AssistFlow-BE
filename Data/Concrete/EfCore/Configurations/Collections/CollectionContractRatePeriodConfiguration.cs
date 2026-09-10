using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionContractRatePeriodConfiguration : IEntityTypeConfiguration<CollectionContractRatePeriod>
{
    public void Configure(EntityTypeBuilder<CollectionContractRatePeriod> builder)
    {
        builder.ToTable("ContractRatePeriod", "collection", table =>
        {
            table.HasCheckConstraint("CK_ContractRatePeriod_Dates", "[EffectiveToExclusive] IS NULL OR [EffectiveToExclusive] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_ContractRatePeriod_Anchor", "[BillingAnchor] <= [EffectiveFrom]");
            table.HasCheckConstraint("CK_ContractRatePeriod_Amount", "[Amount] IS NULL OR [Amount] >= 0");
            table.HasCheckConstraint("CK_ContractRatePeriod_Behavior", "[BillingBehavior] IN (0,1,2)");
            table.HasCheckConstraint("CK_ContractRatePeriod_Billable", "[BillingBehavior] <> 0 OR ([Amount] IS NOT NULL AND [CurrencyTypeId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.EffectiveFrom).HasColumnType("date");
        builder.Property(x => x.EffectiveToExclusive).HasColumnType("date");
        builder.Property(x => x.BillingAnchor).HasColumnType("date");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.BillingBehavior).HasConversion<byte>();
        builder.Property(x => x.ChangeReason).HasMaxLength(100);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PaymentFrequency).WithMany().HasForeignKey(x => x.PaymentFrequencyId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.CurrencyType).WithMany().HasForeignKey(x => x.CurrencyTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ContractId, x.EffectiveFrom }).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_ContractRatePeriod_Start");
        builder.HasIndex(x => x.ContractId).IsUnique()
            .HasFilter("[EffectiveToExclusive] IS NULL AND [IsDeleted] = 0").HasDatabaseName("UX_ContractRatePeriod_Open");
    }
}
