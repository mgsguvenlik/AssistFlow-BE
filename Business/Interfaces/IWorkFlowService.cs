using Core.Common;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowStatus;

namespace Business.Interfaces
{
    public interface IWorkFlowService
    {
        // ServicesRequest
        Task<ResponseModel<PagedResult<ServicesRequestGetDto>>> GetRequestsAsync(QueryParams q);
        Task<ResponseModel<ServicesRequestGetDto>> GetRequestByIdAsync(long id);
        Task<ResponseModel<ServicesRequestGetDto>> CreateRequestAsync(ServicesRequestCreateDto dto);
        Task<ResponseModel<ServicesRequestGetDto>> UpdateRequestAsync(ServicesRequestUpdateDto dto);
        Task<ResponseModel> DeleteRequestAsync(long id);

        // ServicesRequest - ürün yönetimi
        Task<ResponseModel<ServicesRequestGetDto>> ReplaceRequestProductsAsync(long requestId, IEnumerable<long> productIds);

        // WorkFlowStatus
        Task<ResponseModel<PagedResult<WorkFlowStatusGetDto>>> GetStatusesAsync(QueryParams q);
        Task<ResponseModel<WorkFlowStatusGetDto>> GetStatusByIdAsync(long id);
        Task<ResponseModel<WorkFlowStatusGetDto>> CreateStatusAsync(WorkFlowStatusCreateDto dto);
        Task<ResponseModel<WorkFlowStatusGetDto>> UpdateStatusAsync(WorkFlowStatusUpdateDto dto);
        Task<ResponseModel> DeleteStatusAsync(long id);

        // WorkFlow (tanım)

        Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "SR");
        Task<ResponseModel<PagedResult<WorkFlowGetDto>>> GetWorkFlowsAsync(QueryParams q);
        Task<ResponseModel<WorkFlowGetDto>> GetWorkFlowByIdAsync(long id);
        Task<ResponseModel<WorkFlowGetDto>> CreateWorkFlowAsync(WorkFlowCreateDto dto);
        Task<ResponseModel<WorkFlowGetDto>> UpdateWorkFlowAsync(WorkFlowUpdateDto dto);
        Task<ResponseModel> DeleteWorkFlowAsync(long id);


        // Warehouse (depo) ile ilgili işlemler 
        Task<ResponseModel<ServicesRequestGetDto>> SendWarehouseAsync(SendWarehouseDto dto);
    }
}
