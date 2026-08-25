namespace Business.Interfaces.PeriodicReports
{
    public interface IPeriodicReportScheduleCalculator
    {
        bool IsValid(string cronExpression, string timeZoneId, out string? error);
        DateTimeOffset? GetNextOccurrenceUtc(string cronExpression, string timeZoneId, DateTimeOffset fromUtc);
    }
}
