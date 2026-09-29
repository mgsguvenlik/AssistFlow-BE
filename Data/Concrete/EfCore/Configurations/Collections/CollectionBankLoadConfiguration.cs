using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionBankLoadConfiguration : IEntityTypeConfiguration<CollectionBankLoad>
{
    public void Configure(EntityTypeBuilder<CollectionBankLoad> b)
    {
        b.ToTable("BankLoad", "collection"); b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(3); b.Property(x => x.FileHash).HasMaxLength(64);
        b.Property(x => x.FileName).HasMaxLength(260); b.Property(x => x.StoredFileName).HasMaxLength(260);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Type, x.FileHash, x.Period }).IsUnique();
    }
}
public sealed class CollectionBankLoadRowConfiguration : IEntityTypeConfiguration<CollectionBankLoadRow>
{
    public void Configure(EntityTypeBuilder<CollectionBankLoadRow> b)
    {
        b.ToTable("BankLoadRow", "collection"); b.HasKey(x => x.Id);
        b.Property(x => x.TransactionKey).HasMaxLength(64); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Issue).HasMaxLength(4000);
        b.HasOne(x => x.Load).WithMany(x => x.Rows).HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.LoadId, x.RowNumber }).IsUnique();
        b.HasIndex(x => new { x.LoadId, x.Status, x.RowNumber });
    }
}
public sealed class CollectionBankTransactionConfiguration : IEntityTypeConfiguration<CollectionBankTransaction>
{
    public void Configure(EntityTypeBuilder<CollectionBankTransaction> b)
    {
        b.ToTable("BankTransaction", "collection"); b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(64); b.Property(x => x.Source).HasMaxLength(20);
    }
}
public sealed class CollectionBankBaselineConfiguration : IEntityTypeConfiguration<CollectionBankBaseline>
{
    public void Configure(EntityTypeBuilder<CollectionBankBaseline> b)
    {
        b.ToTable("BankBaseline", "collection"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SourceHash).HasMaxLength(64);
    }
}
