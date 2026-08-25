using Business.Models;

namespace Business.Interfaces.PeriodicReports
{
    public interface IReportQueryExecutor
    {
        Task<ReportData> ExecuteAsync(
            string sqlQuery,
            int maxRows,
            bool allowTruncation,
            CancellationToken cancellationToken);
    }
}
