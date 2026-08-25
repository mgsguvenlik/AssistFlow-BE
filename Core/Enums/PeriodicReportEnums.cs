namespace Core.Enums
{
    public enum PeriodicReportOutputFormat
    {
        Excel = 1,
        Csv = 2,
        Html = 3,
        Pdf = 4
    }

    public enum PeriodicReportExecutionStatus
    {
        Running = 1,
        Success = 2,
        Failed = 3
    }

    public enum PeriodicReportTriggerType
    {
        Scheduled = 1,
        Manual = 2
    }
}
