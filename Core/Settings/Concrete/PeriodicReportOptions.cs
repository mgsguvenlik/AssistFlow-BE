namespace Core.Settings.Concrete
{
    public sealed class PeriodicReportOptions
    {
        public const string SectionName = "PeriodicReport";

        public string? ReportingConnectionString { get; set; }
        public string TimeZoneId { get; set; } = "Europe/Istanbul";
        public int SchedulerPollSeconds { get; set; } = 30;
        public int SchedulerBatchSize { get; set; } = 10;
        public int QueryTimeoutSeconds { get; set; } = 120;
        public int MaxRows { get; set; } = 50_000;
        public int PreviewMaxRows { get; set; } = 100;
        public int MaxResultSizeMb { get; set; } = 30;
        public int MaxAttachmentSizeMb { get; set; } = 20;
        public int ExecutionLeaseMinutes { get; set; } = 30;
    }
}
