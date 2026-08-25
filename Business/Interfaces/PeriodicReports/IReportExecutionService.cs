using Business.Models;
using Core.Enums;

namespace Business.Interfaces.PeriodicReports
{
    public interface IReportExecutionService
    {
        Task<ReportExecutionOutcome> ExecuteAsync(
            long reportId,
            PeriodicReportTriggerType triggerType,
            long? triggeredByUserId,
            CancellationToken cancellationToken);
    }
}
