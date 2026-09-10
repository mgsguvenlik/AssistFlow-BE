using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionContractPeriodFollowUpConfiguration : IEntityTypeConfiguration<CollectionContractPeriodFollowUp>
{
    public void Configure(EntityTypeBuilder<CollectionContractPeriodFollowUp> builder)
    {
        builder.ToTable("ContractPeriodFollowUp", "collection", table =>
            table.HasCheckConstraint("CK_ContractPeriodFollowUp_Period", "DAY([Period]) = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Period).HasColumnType("date");
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.GroupStatus).WithMany().HasForeignKey(x => x.GroupStatusId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ContractId, x.Period }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ContractPeriodFollowUp_Contract_Period");
    }
}
