using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionPaymentConfiguration : IEntityTypeConfiguration<CollectionPayment>
{
    public void Configure(EntityTypeBuilder<CollectionPayment> builder)
    {
        builder.ToTable("Payment", "collection", table =>
            table.HasCheckConstraint("CK_Payment_Period", "DAY([Period]) = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Period).HasColumnType("date");
        builder.Property(x => x.PaymentDate).HasColumnType("date");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.CurrencyType).WithMany().HasForeignKey(x => x.CurrencyTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ContractId, x.Period, x.CurrencyTypeId, x.Id }).HasDatabaseName("IX_Payment_Contract_Period_Currency_Id");
        // Negative legacy refunds remain representable; command validation determines allowed new inputs.
    }
}
