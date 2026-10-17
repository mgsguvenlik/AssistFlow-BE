using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

/// <summary>Not registered in the active context. No seed or activation decisions are applied.</summary>
public sealed class CollectionPaymentFrequencyConfiguration : IEntityTypeConfiguration<CollectionPaymentFrequency>
{
    public void Configure(EntityTypeBuilder<CollectionPaymentFrequency> builder)
    {
        builder.ToTable("PaymentFrequency", "collection", table =>
            table.HasCheckConstraint("CK_PaymentFrequency_IntervalMonths", "[IntervalMonths] IN (1,2,3,4,6,12,24,36)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IntervalMonths).HasColumnType("smallint");
        builder.Property(x => x.DisplayOrder).HasColumnType("smallint");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_PaymentFrequency_Code");
        builder.HasIndex(x => x.IntervalMonths).IsUnique().HasDatabaseName("UX_PaymentFrequency_IntervalMonths");
    }
}
