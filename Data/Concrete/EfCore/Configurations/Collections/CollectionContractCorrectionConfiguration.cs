using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;
namespace Data.Concrete.EfCore.Configurations.Collections;
public sealed class CollectionContractCorrectionConfiguration : IEntityTypeConfiguration<CollectionContractCorrection>
{
    public void Configure(EntityTypeBuilder<CollectionContractCorrection> b)
    {
        b.ToTable("ContractCorrection", "collection"); b.HasKey(x => x.Id);
        b.Property(x => x.PayloadHash).HasMaxLength(64); b.Property(x => x.Kind).HasMaxLength(20); b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => x.RequestId).IsUnique(); b.HasIndex(x => new { x.ContractId, x.Id });
    }
}
