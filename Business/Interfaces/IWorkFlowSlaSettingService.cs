using Core.Common;
using Core.Enums;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting;

namespace Business.Interfaces
{
    public interface IWorkFlowSlaSettingService
    {
        /// <summary>
        /// Belirli bir CustomerType ve Priority için SLA ayarýný getirir
        /// </summary>
        Task<ResponseModel<WorkFlowSlaSetting?>> GetSlaSettingAsync(WorkFlowCustomerType customerType, WorkFlowPriority priority);

        Task<ResponseModel<PagedResult<WorkFlowSlaSettingGetDto>>> GetFilteredPagedAsync(WorkFlowSlaSettingQueryParams q);

        Task<ResponseModel<List<WorkFlowSlaSettingGetDto>>> GetByCustomerTypeAsync( WorkFlowCustomerType customerType);
    }
}