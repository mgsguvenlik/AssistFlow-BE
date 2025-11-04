using Core.Common;
using Model.Dtos.WorkFlowDtos.Pricing;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using Model.Dtos.WorkFlowDtos.WorkFlowStep;

namespace Business.Interfaces
{
    public interface IWorkFlowService
    {
        // ServicesRequest
        Task<ResponseModel<PagedResult<ServicesRequestGetDto>>> GetRequestsAsync(QueryParams q);
        Task<ResponseModel<ServicesRequestGetDto>> GetServiceRequestByNoAsync(string requestNo);
      
        Task<ResponseModel<ServicesRequestGetDto>> GetServiceRequestByIdAsync(long id);
        Task<ResponseModel<ServicesRequestGetDto>> CreateRequestAsync(ServicesRequestCreateDto dto);
        Task<ResponseModel<ServicesRequestGetDto>> UpdateServiceRequestAsync(ServicesRequestUpdateDto dto);
        Task<ResponseModel> DeleteRequestAsync(long id);
        Task<ResponseModel<TechnicalServiceGetDto>> SendTechnicalServiceAsync(SendTechnicalServiceDto dto);
        Task<ResponseModel<TechnicalServiceGetDto>> StartService(StartTechnicalServiceDto dto);
        Task<ResponseModel<TechnicalServiceGetDto>> FinishService(FinishTechnicalServiceDto dto);
        Task<ResponseModel<PricingGetDto>> ApprovePricing(PricingUpdateDto dto);
        Task<ResponseModel> RequestLocationOverrideAsync(OverrideLocationCheckDto dto);


        Task<ResponseModel<WorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes);

    

        // WorkFlowStep
        Task<ResponseModel<PagedResult<WorkFlowStepGetDto>>> GetStepsAsync(QueryParams q);
        Task<ResponseModel<WorkFlowStepGetDto>> GetStepByIdAsync(long id);
        Task<ResponseModel<WorkFlowStepGetDto>> CreateStepAsync(WorkFlowStepCreateDto dto);
        Task<ResponseModel<WorkFlowStepGetDto>> UpdateStepAsync(WorkFlowStepUpdateDto dto);
        Task<ResponseModel> DeleteStepAsync(long id);

        // WorkFlow (tanım)

        Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "SR");
        Task<ResponseModel<PagedResult<WorkFlowGetDto>>> GetWorkFlowsAsync(QueryParams q);
        Task<ResponseModel<WorkFlowGetDto>> GetWorkFlowByIdAsync(long id);
        Task<ResponseModel<WorkFlowGetDto>> CreateWorkFlowAsync(WorkFlowCreateDto dto);
        Task<ResponseModel<WorkFlowGetDto>> UpdateWorkFlowAsync(WorkFlowUpdateDto dto);
        Task<ResponseModel> DeleteWorkFlowAsync(long id);
        Task<ResponseModel> CancelWorkFlowAsync(long id);

        // Warehouse (depo) ile ilgili işlemler 
        Task<ResponseModel<WarehouseGetDto>> SendWarehouseAsync(SendWarehouseDto dto);
        Task<ResponseModel<WarehouseGetDto>> GetWarehouseByIdAsync(long id);
        Task<ResponseModel<WarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo);
        Task<ResponseModel<WarehouseGetDto>> CompleteDeliveryAsync(CompleteDeliveryDto dto);

        //Teknik Servis ile ilgili işlemler eklenecek
        Task<ResponseModel<TechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo);

    }
}
