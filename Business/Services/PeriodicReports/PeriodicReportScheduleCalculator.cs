using Business.Interfaces.PeriodicReports;
using Cronos;

namespace Business.Services.PeriodicReports
{
    public sealed class PeriodicReportScheduleCalculator : IPeriodicReportScheduleCalculator
    {
        public bool IsValid(string cronExpression, string timeZoneId, out string? error)
        {
            try
            {
                var next = GetNextOccurrenceUtc(cronExpression, timeZoneId, DateTimeOffset.UtcNow);
                if (!next.HasValue)
                {
                    error = "Cron ifadesi gelecekte bir çalışma zamanı üretmiyor.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception ex) when (ex is CronFormatException or TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                error = "Cron veya saat dilimi geçersiz.";
                return false;
            }
        }

        public DateTimeOffset? GetNextOccurrenceUtc(
            string cronExpression,
            string timeZoneId,
            DateTimeOffset fromUtc)
        {
            var expression = CronExpression.Parse(cronExpression.Trim(), CronFormat.Standard);
            var timeZone = ResolveTimeZone(timeZoneId);
            return expression.GetNextOccurrence(fromUtc.ToUniversalTime(), timeZone)?.ToUniversalTime();
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            var requested = string.IsNullOrWhiteSpace(timeZoneId)
                ? "Europe/Istanbul"
                : timeZoneId.Trim();

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(requested);
            }
            catch (TimeZoneNotFoundException) when (
                requested.Equals("Europe/Istanbul", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
        }
    }
}
