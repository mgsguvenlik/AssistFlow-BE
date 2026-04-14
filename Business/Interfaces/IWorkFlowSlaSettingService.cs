using Core.Common;
using Core.Enums;
using Model.Concrete.WorkFlows;

namespace Business.Interfaces
{
    public interface IWorkFlowSlaSettingService
    {
        /// <summary>
        /// Belirli bir CustomerType ve Priority için SLA ayarýný getirir
        /// </summary>
        Task<ResponseModel<WorkFlowSlaSetting?>> GetSlaSettingAsync(WorkFlowCustomerType customerType, WorkFlowPriority priority);
    }
}