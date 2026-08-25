using Business.Models;
using Core.Enums;

namespace Business.Interfaces.PeriodicReports
{
    public interface IReportExporter
    {
        PeriodicReportOutputFormat Format { get; }
        Task<ReportFile> ExportAsync(string reportName, ReportData data, CancellationToken cancellationToken);
    }
}
