using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.WorkFlows;

namespace Data.Concrete.EfCore.Configurations
{
    public class WorkFlowSlaSettingConfiguration : IEntityTypeConfiguration<WorkFlowSlaSetting>
    {
        public void Configure(EntityTypeBuilder<WorkFlowSlaSetting> builder)
        {
            builder.ToTable("WorkFlowSlaSettings");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.CustomerType)
                .IsRequired()
                .HasComment("Müþteri/Ýþ birimi tipi (General, Ykb, Individual, Corporate)");

            builder.Property(x => x.Priority)
                .IsRequired()
                .HasComment("Ýþ akýþý öncelik seviyesi");

            builder.Property(x => x.SlaDurationDays)
                .IsRequired()
                .HasComment("SLA süresi (gün)");

            builder.Property(x => x.NotificationBeforeDays)
                .IsRequired()
                .HasComment("Bildirim gönderilecek süre (gün önce)");

            builder.Property(x => x.NotificationEmails)
                .HasMaxLength(1000)
                .HasComment("Bildirim gönderilecek e-posta adresleri (virgülle ayrýlmýþ)");


            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true)
                .HasComment("Aktif mi");

            builder.Property(x => x.Description)
                .HasMaxLength(500)
                .HasComment("Açýklama");

            builder.HasIndex(x => new { x.CustomerType, x.Priority })
                .IsUnique()
                .HasDatabaseName("IX_WorkFlowSlaSettings_CustomerType_Priority");

            builder.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_WorkFlowSlaSettings_IsActive");
        }
    }
}