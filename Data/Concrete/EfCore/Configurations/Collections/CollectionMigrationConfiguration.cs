using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionMigrationBatchConfiguration : IEntityTypeConfiguration<CollectionMigrationBatch>
{
    public void Configure(EntityTypeBuilder<CollectionMigrationBatch> b)
    {
        b.ToTable("MigrationBatch", "collection", t =>
        {
            t.HasCheckConstraint("CK_MigrationBatch_Status", "[Status] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_MigrationBatch_Hash", "DATALENGTH([ManifestHash]) = 32");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.SourceSystem).HasMaxLength(50).IsRequired();
        b.Property(x => x.SnapshotKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.ManifestHash).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.RuleVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.NormalizationVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasConversion<byte>(); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.SourceSystem, x.SnapshotKey }).IsUnique().HasDatabaseName("UX_MigrationBatch_Source_Snapshot");
    }
}

public sealed class CollectionMigrationSourceRowConfiguration : IEntityTypeConfiguration<CollectionMigrationSourceRow>
{
    public void Configure(EntityTypeBuilder<CollectionMigrationSourceRow> b)
    {
        b.ToTable("MigrationSourceRow", "collection", t =>
        {
            t.HasCheckConstraint("CK_MigrationSourceRow_Hash", "DATALENGTH([PayloadHash]) = 32");
            t.HasCheckConstraint("CK_MigrationSourceRow_Payload", "ISJSON([Payload]) = 1");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.EntityCode).HasMaxLength(40).IsRequired(); b.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        b.Property(x => x.SourceParentId).HasMaxLength(100); b.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.PayloadHash).HasColumnType("binary(32)").IsRequired();
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.BatchId, x.EntityCode, x.SourceId }).IsUnique().HasDatabaseName("UX_MigrationSourceRow_Batch_Entity_Source");
        b.HasIndex(x => new { x.BatchId, x.EntityCode, x.Id }).HasDatabaseName("IX_MigrationSourceRow_Batch_Entity_Id");
        b.HasIndex(x => new { x.BatchId, x.EntityCode, x.SourceParentId, x.Id }).HasDatabaseName("IX_MigrationSourceRow_Batch_Parent_Id");
    }
}

public sealed class CollectionMigrationContractStageConfiguration : IEntityTypeConfiguration<CollectionMigrationContractStage>
{
    public void Configure(EntityTypeBuilder<CollectionMigrationContractStage> b)
    {
        b.ToTable("MigrationContractStage", "collection", t => t.HasCheckConstraint("CK_MigrationContractStage_Status", "[Status] BETWEEN 0 AND 5"));
        b.HasKey(x => x.SourceRowId); b.Property(x => x.SourceRowId).ValueGeneratedNever(); b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.SourceCustomerId).HasMaxLength(100); b.Property(x => x.SubscriberNoRaw).HasMaxLength(200);
        b.Property(x => x.SubscriberNoNormalized).HasMaxLength(200); b.Property(x => x.SourceServiceTypeId).HasMaxLength(100);
        b.Property(x => x.SourceContractStatusId).HasMaxLength(100); b.Property(x => x.SourceSubscriptionStatusId).HasMaxLength(100);
        b.Property(x => x.SourcePaymentMethodId).HasMaxLength(100); b.Property(x => x.EndDate).HasColumnType("date");
        b.Property(x => x.GtsNo).HasMaxLength(50); b.Property(x => x.IvrNo).HasMaxLength(50);
        b.Property(x => x.AttachmentName).HasMaxLength(255); b.Property(x => x.AttachmentPath).HasMaxLength(1000); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.SourceRow).WithOne().HasForeignKey<CollectionMigrationContractStage>(x => x.SourceRowId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetCustomer).WithMany().HasForeignKey(x => x.TargetCustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetServiceType).WithMany().HasForeignKey(x => x.TargetServiceTypeId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.Status, x.SourceRowId }).HasDatabaseName("IX_MigrationContractStage_Status_Row");
        b.HasIndex(x => x.SubscriberNoNormalized).HasDatabaseName("IX_MigrationContractStage_Subscriber");
    }
}

public sealed class CollectionMigrationRatePeriodStageConfiguration : IEntityTypeConfiguration<CollectionMigrationRatePeriodStage>
{
    public void Configure(EntityTypeBuilder<CollectionMigrationRatePeriodStage> b)
    {
        b.ToTable("MigrationRatePeriodStage", "collection", t =>
        {
            t.HasCheckConstraint("CK_MigrationRateStage_Status", "[Status] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_MigrationRateStage_Behavior", "[BillingBehavior] IS NULL OR [BillingBehavior] IN (0,1,2)");
        });
        b.HasKey(x => x.SourceRowId); b.Property(x => x.SourceRowId).ValueGeneratedNever(); b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.BillingBehavior).HasConversion<byte>(); b.Property(x => x.SourceContractId).HasMaxLength(100);
        b.Property(x => x.SourceCustomerId).HasMaxLength(100); b.Property(x => x.EffectiveFrom).HasColumnType("date");
        b.Property(x => x.EffectiveToExclusive).HasColumnType("date"); b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        b.Property(x => x.SourceCurrencyId).HasMaxLength(100); b.Property(x => x.SourcePaymentTypeId).HasMaxLength(100);
        b.Property(x => x.ProcessType).HasMaxLength(100); b.Property(x => x.Description).HasMaxLength(1000); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.SourceRow).WithOne().HasForeignKey<CollectionMigrationRatePeriodStage>(x => x.SourceRowId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetCurrencyType).WithMany().HasForeignKey(x => x.TargetCurrencyTypeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetPaymentFrequency).WithMany().HasForeignKey(x => x.TargetPaymentFrequencyId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.Status, x.SourceRowId }).HasDatabaseName("IX_MigrationRateStage_Status_Row");
        b.HasIndex(x => new { x.SourceContractId, x.EffectiveFrom, x.SourceRowId }).HasDatabaseName("IX_MigrationRateStage_Contract_Date_Row");
    }
}

public sealed class CollectionMigrationReferenceMapConfiguration : IEntityTypeConfiguration<CollectionMigrationReferenceMap>
{
    public void Configure(EntityTypeBuilder<CollectionMigrationReferenceMap> b)
    {
        b.ToTable("MigrationReferenceMap", "collection", t =>
        {
            t.HasCheckConstraint("CK_MigrationReferenceMap_Status", "[Status] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_MigrationReferenceMap_Target", "[Status] <> 1 OR (CASE WHEN [TargetCustomerId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCustomerTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetServiceTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCurrencyTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentFrequencyId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentMethodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetSubscriptionStatusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetContractStatusId] IS NULL THEN 0 ELSE 1 END) = 1");
            t.HasCheckConstraint("CK_MigrationReferenceMap_Kind", "[Status] <> 1 OR ([ReferenceKind] = N'Customer' AND [TargetCustomerId] IS NOT NULL) OR ([ReferenceKind] = N'CustomerType' AND [TargetCustomerTypeId] IS NOT NULL) OR ([ReferenceKind] = N'ServiceType' AND [TargetServiceTypeId] IS NOT NULL) OR ([ReferenceKind] = N'CurrencyType' AND [TargetCurrencyTypeId] IS NOT NULL) OR ([ReferenceKind] = N'PaymentFrequency' AND [TargetPaymentFrequencyId] IS NOT NULL) OR ([ReferenceKind] = N'PaymentMethod' AND [TargetPaymentMethodId] IS NOT NULL) OR ([ReferenceKind] = N'SubscriptionStatus' AND [TargetSubscriptionStatusId] IS NOT NULL) OR ([ReferenceKind] = N'ContractStatus' AND [TargetContractStatusId] IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedOnAdd(); b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.ReferenceKind).HasMaxLength(40).IsRequired(); b.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        b.Property(x => x.MatchMethod).HasMaxLength(40).IsRequired(); b.Property(x => x.Evidence).HasMaxLength(1000); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetCustomer).WithMany().HasForeignKey(x => x.TargetCustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetCustomerType).WithMany().HasForeignKey(x => x.TargetCustomerTypeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetServiceType).WithMany().HasForeignKey(x => x.TargetServiceTypeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetCurrencyType).WithMany().HasForeignKey(x => x.TargetCurrencyTypeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetPaymentFrequency).WithMany().HasForeignKey(x => x.TargetPaymentFrequencyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetPaymentMethod).WithMany().HasForeignKey(x => x.TargetPaymentMethodId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetSubscriptionStatus).WithMany().HasForeignKey(x => x.TargetSubscriptionStatusId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetContractStatus).WithMany().HasForeignKey(x => x.TargetContractStatusId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.BatchId, x.ReferenceKind, x.SourceId }).IsUnique().HasDatabaseName("UX_MigrationReferenceMap_Batch_Kind_Source");
        b.HasIndex(x => new { x.Status, x.ReferenceKind, x.Id }).HasDatabaseName("IX_MigrationReferenceMap_Status_Kind_Id");
    }
}

public sealed class CollectionMigrationIssueConfiguration : IEntityTypeConfiguration<CollectionMigrationIssue>
{
    public void Configure(EntityTypeBuilder<CollectionMigrationIssue> b)
    {
        b.ToTable("MigrationIssue", "collection", t =>
        {
            t.HasCheckConstraint("CK_MigrationIssue_Severity", "[Severity] IN (0,1)");
            t.HasCheckConstraint("CK_MigrationIssue_Status", "[Status] BETWEEN 0 AND 2");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedOnAdd(); b.Property(x => x.Severity).HasConversion<byte>(); b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.IssueCode).HasMaxLength(60).IsRequired(); b.Property(x => x.Details).HasMaxLength(2000);
        b.Property(x => x.ResolutionNote).HasMaxLength(2000); b.Property(x => x.RuleVersion).HasMaxLength(50).IsRequired();
        b.HasOne(x => x.SourceRow).WithMany().HasForeignKey(x => x.SourceRowId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.SourceRowId, x.IssueCode }).IsUnique().HasFilter("[Status] = 0").HasDatabaseName("UX_MigrationIssue_Open_Row_Code");
        b.HasIndex(x => new { x.Status, x.IssueCode, x.SourceRowId, x.Id }).HasDatabaseName("IX_MigrationIssue_Status_Code_Row_Id");
    }
}

public sealed class CollectionMigrationMapConfiguration : IEntityTypeConfiguration<CollectionMigrationMap>
{
    public void Configure(EntityTypeBuilder<CollectionMigrationMap> b)
    {
        b.ToTable("MigrationMap", "collection", t =>
        {
            t.HasCheckConstraint("CK_MigrationMap_Hash", "DATALENGTH([AppliedPayloadHash]) = 32");
            t.HasCheckConstraint("CK_MigrationMap_Target", "(CASE WHEN [TargetContractId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetRatePeriodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentId] IS NULL THEN 0 ELSE 1 END) = 1");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedOnAdd(); b.Property(x => x.SourceSystem).HasMaxLength(50).IsRequired();
        b.Property(x => x.EntityCode).HasMaxLength(40).IsRequired(); b.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        b.Property(x => x.AppliedPayloadHash).HasColumnType("binary(32)").IsRequired(); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.TargetContract).WithMany().HasForeignKey(x => x.TargetContractId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetRatePeriod).WithMany().HasForeignKey(x => x.TargetRatePeriodId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.TargetPayment).WithMany().HasForeignKey(x => x.TargetPaymentId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.FirstBatch).WithMany().HasForeignKey(x => x.FirstBatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.LastBatch).WithMany().HasForeignKey(x => x.LastBatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.SourceSystem, x.EntityCode, x.SourceId }).IsUnique().HasDatabaseName("UX_MigrationMap_Source_Entity_Id");
        b.HasIndex(x => x.TargetContractId).IsUnique().HasFilter("[TargetContractId] IS NOT NULL").HasDatabaseName("UX_MigrationMap_TargetContract");
        b.HasIndex(x => x.TargetRatePeriodId).IsUnique().HasFilter("[TargetRatePeriodId] IS NOT NULL").HasDatabaseName("UX_MigrationMap_TargetRate");
        b.HasIndex(x => x.TargetPaymentId).IsUnique().HasFilter("[TargetPaymentId] IS NOT NULL").HasDatabaseName("UX_MigrationMap_TargetPayment");
    }
}
