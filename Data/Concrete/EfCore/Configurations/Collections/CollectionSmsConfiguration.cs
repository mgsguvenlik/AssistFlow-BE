using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Configurations.Collections;

public sealed class CollectionSmsNotificationConfiguration : IEntityTypeConfiguration<CollectionSmsNotification>
{
    public void Configure(EntityTypeBuilder<CollectionSmsNotification> b)
    {
        b.ToTable("SmsNotification", "collection", t =>
        {
            t.HasCheckConstraint("CK_SmsNotification_Status", "[Status] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_SmsNotification_Attempts", "[AttemptCount] > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.OldAmount).HasPrecision(18, 2);
        b.Property(x => x.NewAmount).HasPrecision(18, 2);
        b.Property(x => x.IncreasePercent).HasPrecision(28, 8);
        b.Property(x => x.EffectiveDate).HasColumnType("date");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.RatePeriodId).IsUnique();
        b.HasIndex(x => new { x.Status, x.UpdatedDate, x.Id });
        b.HasIndex(x => new { x.ContractId, x.CreatedDate, x.Id });
        // No FK to financial rows: their approved physical removal must retain notification evidence.
    }
}

public sealed class CollectionSmsAttemptConfiguration : IEntityTypeConfiguration<CollectionSmsAttempt>
{
    public void Configure(EntityTypeBuilder<CollectionSmsAttempt> b)
    {
        b.ToTable("SmsAttempt", "collection", t => t.HasCheckConstraint("CK_SmsAttempt_Status", "[Status] BETWEEN 0 AND 5"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Phone).HasMaxLength(12);
        b.Property(x => x.Message).HasMaxLength(2000);
        b.Property(x => x.Template).HasMaxLength(2000);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.ResultMessage).HasMaxLength(500);
        b.Property(x => x.ErrorCode).HasMaxLength(100);
        b.Property(x => x.PackageId).HasMaxLength(100);
        b.HasOne(x => x.Notification).WithMany(x => x.Attempts).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.NotificationId, x.Sequence }).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedDate, x.Id });
    }
}
