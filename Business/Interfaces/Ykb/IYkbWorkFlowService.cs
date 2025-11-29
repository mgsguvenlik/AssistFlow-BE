using Core.Common;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbFinalApproval;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbPricing;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequest;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWarehouse;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlow;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlowStep;

namespace Business.Interfaces.Ykb
{
    public interface IYkbWorkFlowService
    {
        Task<ResponseModel<YkbServicesRequestGetDto>> SendCustomerFormToService(YkbCustomerFormCreateDto dto);
        Task<ResponseModel<YkbCustomerFormGetDto>> CreateCustomerForm(YkbCustomerFormCreateDto dto);
        Task<ResponseModel<PagedResult<YkbServicesRequestGetDto>>> GetRequestsAsync(QueryParams q);
        Task<ResponseModel<YkbServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo);

        Task<ResponseModel<YkbServicesRequestGetDto>> GetServiceRequestByIdAsync(long id);
        Task<ResponseModel<YkbServicesRequestGetDto>> CreateRequestAsync(YkbServicesRequestCreateDto dto);
        Task<ResponseModel<YkbServicesRequestGetDto>> UpdateServiceRequestAsync(YkbServicesRequestUpdateDto dto);
        Task<ResponseModel> DeleteRequestAsync(long id);
        Task<ResponseModel<YkbTechnicalServiceGetDto>> SendTechnicalServiceAsync(YkbSendTechnicalServiceDto dto);
        Task<ResponseModel<YkbTechnicalServiceGetDto>> StartService(YkbStartTechnicalServiceDto dto);
        Task<ResponseModel<YkbTechnicalServiceGetDto>> FinishService(YkbFinishTechnicalServiceDto dto);
        Task<ResponseModel<YkbPricingGetDto>> ApprovePricing(YkbPricingUpdateDto dto);
        Task<ResponseModel<YkbPricingGetDto>> GetPricingByRequestNoAsync(string requestNo);
        Task<ResponseModel> RequestLocationOverrideAsync(YkbOverrideLocationCheckDto dto);

        Task<ResponseModel<YkbWorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes);

        Task<ResponseModel<YkbFinalApprovalGetDto>> FinalApprovalAsync(YkbFinalApprovalUpdateDto dto);
        Task<ResponseModel<YkbFinalApprovalGetDto>> GetFinalApprovalByRequestNoAsync(string requestNo);
        Task<ResponseModel<YkbFinalApprovalGetDto>> GetFinalApprovalByIdAsync(long id);

        // WorkFlowStep
        Task<ResponseModel<PagedResult<YkbWorkFlowStepGetDto>>> GetStepsAsync(QueryParams q);
        Task<ResponseModel<YkbWorkFlowStepGetDto>> GetStepByIdAsync(long id);
        Task<ResponseModel<YkbWorkFlowStepGetDto>> CreateStepAsync(YkbWorkFlowStepCreateDto dto);
        Task<ResponseModel<YkbWorkFlowStepGetDto>> UpdateStepAsync(YkbWorkFlowStepUpdateDto dto);
        Task<ResponseModel> DeleteStepAsync(long id);

        // WorkFlow (tanım)

        Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "SR");
        Task<ResponseModel<PagedResult<YkbWorkFlowGetDto>>> GetWorkFlowsAsync(QueryParams q);
        Task<ResponseModel> DeleteWorkFlowAsync(long id);
        Task<ResponseModel> CancelWorkFlowAsync(long id);

        // Warehouse (depo) ile ilgili işlemler 
        Task<ResponseModel<YkbWarehouseGetDto>> SendWarehouseAsync(YkbSendWarehouseDto dto);
        Task<ResponseModel<YkbWarehouseGetDto>> GetWarehouseByIdAsync(long id);
        Task<ResponseModel<YkbWarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo);
        Task<ResponseModel<YkbWarehouseGetDto>> CompleteDeliveryAsync(YkbCompleteDeliveryDto dto);

        //Teknik Servis ile ilgili işlemler eklenecek
        Task<ResponseModel<YkbTechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo);


        // Report 

        //Task<ResponseModel<PagedResult<WorkFlowReportListItemDto>>> GetReportsAsync(ReportQueryParams q);
        Task<ResponseModel<YkbWorkFlowReportDto>> GetReportAsync(string requestNo);

        Task<PagedResult<YkbWorkFlowReportListItemDto>> GetReportsAsync(YkbReportQueryParams q);
        Task<PagedResult<YkbWorkFlowReportLineDto>> GetReportLinesAsync(YkbReportQueryParams q);

        Task<(byte[] Content, string FileName, string ContentType)> ExportReportLinesAsync(YkbReportQueryParams q);


        //Arşiv 

        Task<ResponseModel<PagedResult<YkbWorkFlowArchiveListDto>>> GetArchiveListAsync(YkbWorkFlowArchiveFilterDto filter);
        Task<ResponseModel<YkbWorkFlowArchiveDetailDto>> GetArchiveDetailByIdAsync(long id);
        Task<ResponseModel<YkbWorkFlowArchiveDetailDto>> GetArchiveDetailByRequestNoAsync(string requestNo);
    }
}
