using Core.Common;
using Model.Dtos.PeriodicReports;

namespace Business.Interfaces.PeriodicReports
{
    public interface IPeriodicReportService
    {
        Task<ResponseModel<PagedResult<PeriodicReportListItemDto>>> GetPagedAsync(QueryParams query, CancellationToken cancellationToken);
        Task<ResponseModel<PeriodicReportDetailDto>> GetByIdAsync(long id, CancellationToken cancellationToken);
        Task<ResponseModel<PeriodicReportDetailDto>> CreateAsync(PeriodicReportUpsertDto dto, CancellationToken cancellationToken);
        Task<ResponseModel<PeriodicReportDetailDto>> UpdateAsync(long id, PeriodicReportUpsertDto dto, CancellationToken cancellationToken);
        Task<ResponseModel<bool>> DeleteAsync(long id, CancellationToken cancellationToken);
        Task<ResponseModel<DynamicReportDataDto>> PreviewAsync(PeriodicReportPreviewRequestDto dto, CancellationToken cancellationToken);
        Task<ResponseModel<PagedResult<PeriodicReportExecutionDto>>> GetExecutionsAsync(long id, QueryParams query, CancellationToken cancellationToken);
    }
}
