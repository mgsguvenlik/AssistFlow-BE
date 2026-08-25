using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Model.Concrete.PeriodicReports;

namespace Data.Concrete.EfCore.Configurations
{
    public sealed class PeriodicReportConfiguration : IEntityTypeConfiguration<PeriodicReport>
    {
        public void Configure(EntityTypeBuilder<PeriodicReport> builder)
        {
            builder.ToTable("PeriodicReports");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.SqlQuery).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(x => x.CronExpression).IsRequired().HasMaxLength(100);
            builder.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(100);
            builder.Property(x => x.LastErrorMessage).HasMaxLength(4000);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);

            builder.HasIndex(x => x.Name)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_PeriodicReports_Name_Active");

            builder.HasIndex(x => new { x.IsActive, x.IsDeleted, x.NextRunAtUtc })
                .HasDatabaseName("IX_PeriodicReports_Due");
        }
    }

    public sealed class PeriodicReportRecipientConfiguration : IEntityTypeConfiguration<PeriodicReportRecipient>
    {
        public void Configure(EntityTypeBuilder<PeriodicReportRecipient> builder)
        {
            builder.ToTable("PeriodicReportRecipients");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EmailAddress).IsRequired().HasMaxLength(320);
            builder.HasIndex(x => new { x.PeriodicReportId, x.EmailAddress })
                .IsUnique()
                .HasDatabaseName("UX_PeriodicReportRecipients_Report_Email");

            builder.HasOne(x => x.PeriodicReport)
                .WithMany(x => x.Recipients)
                .HasForeignKey(x => x.PeriodicReportId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class PeriodicReportExecutionConfiguration : IEntityTypeConfiguration<PeriodicReportExecution>
    {
        public void Configure(EntityTypeBuilder<PeriodicReportExecution> builder)
        {
            builder.ToTable("PeriodicReportExecutions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.FileName).HasMaxLength(260);
            builder.Property(x => x.ErrorMessage).HasMaxLength(4000);

            builder.HasIndex(x => new { x.PeriodicReportId, x.StartedAtUtc })
                .HasDatabaseName("IX_PeriodicReportExecutions_Report_StartedAt");
            builder.HasIndex(x => new { x.PeriodicReportId, x.Status })
                .HasDatabaseName("IX_PeriodicReportExecutions_Report_Status");

            builder.HasOne(x => x.PeriodicReport)
                .WithMany(x => x.Executions)
                .HasForeignKey(x => x.PeriodicReportId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
