using Core.Common;
using Core.Enums;
using Model.Dtos.WorkFlowDtos;
using Model.Dtos.WorkFlowDtos.FinalApproval;
using Model.Dtos.WorkFlowDtos.Pricing;
using Model.Dtos.WorkFlowDtos.Report;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlow.Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using Model.Dtos.WorkFlowDtos.WorkFlowArchive;
using Model.Dtos.WorkFlowDtos.WorkFlowStep;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport;

namespace Business.Interfaces
{
    public interface IWorkFlowService
    {
        // ServicesRequest
        Task<ResponseModel<PagedResult<ServicesRequestGetDto>>> GetRequestsAsync(QueryParams q);
        Task<ResponseModel<ServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo);

        Task<ResponseModel<ServicesRequestGetDto>> GetServiceRequestByIdAsync(long id);
        Task<ResponseModel<ServicesRequestGetDto>> CreateRequestAsync(ServicesRequestCreateDto dto);
        Task<ResponseModel<ServicesRequestGetDto>> UpdateServiceRequestAsync(ServicesRequestUpdateDto dto);
        Task<ResponseModel> DeleteRequestAsync(long id);
        Task<ResponseModel<TechnicalServiceGetDto>> SendTechnicalServiceAsync(SendTechnicalServiceDto dto);
        Task<ResponseModel<TechnicalServiceGetDto>> StartService(StartTechnicalServiceDto dto);
        Task<ResponseModel<TechnicalServiceGetDto>> FinishService(FinishTechnicalServiceDto dto);
        Task<ResponseModel<PricingGetDto>> ApprovePricing(PricingUpdateDto dto);
        Task<ResponseModel<PricingGetDto>> GetPricingByRequestNoAsync(string requestNo);
        Task<ResponseModel> RequestLocationOverrideAsync(OverrideLocationCheckDto dto);

        Task<ResponseModel<WorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes);

        Task<ResponseModel<FinalApprovalGetDto>> FinalApprovalAsync(FinalApprovalUpdateDto dto);
        Task<ResponseModel<FinalApprovalGetDto>> GetFinalApprovalByRequestNoAsync(string requestNo);
        Task<ResponseModel<FinalApprovalGetDto>> GetFinalApprovalByIdAsync(long id);

        // WorkFlowStep
        Task<ResponseModel<PagedResult<WorkFlowStepGetDto>>> GetStepsAsync(QueryParams q);
        Task<ResponseModel<WorkFlowStepGetDto>> GetStepByIdAsync(long id);
        Task<ResponseModel<WorkFlowStepGetDto>> CreateStepAsync(WorkFlowStepCreateDto dto);
        Task<ResponseModel<WorkFlowStepGetDto>> UpdateStepAsync(WorkFlowStepUpdateDto dto);
        Task<ResponseModel> DeleteStepAsync(long id);

        // WorkFlow (tanım)

        Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "SR");
        Task<ResponseModel<PagedResult<WorkFlowGetDto>>> GetWorkFlowsAsync(WorkFlowQueryParams q);
        Task<ResponseModel> DeleteWorkFlowAsync(long id);
        Task<ResponseModel> CancelWorkFlowAsync(long id);

        // Warehouse (depo) ile ilgili işlemler 
        Task<ResponseModel<WarehouseGetDto>> SendWarehouseAsync(SendWarehouseDto dto);
        Task<ResponseModel<WarehouseGetDto>> GetWarehouseByIdAsync(long id);
        Task<ResponseModel<WarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo);
        Task<ResponseModel<WarehouseGetDto>> CompleteDeliveryAsync(CompleteDeliveryDto dto);

        //Teknik Servis ile ilgili işlemler eklenecek
        Task<ResponseModel<TechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo);
        Task<ResponseModel> DeleteTechnicalServiceImageAsync(long id, TechnicalServiceImageType type, CancellationToken cancellationToken = default);

        // Report 

        //Task<ResponseModel<PagedResult<WorkFlowReportListItemDto>>> GetReportsAsync(ReportQueryParams q);
        Task<ResponseModel<WorkFlowReportDto>> GetReportAsync(string requestNo);
        Task<PagedResult<WorkFlowReportListItemDto>> GetReportsAsync(ReportQueryParams q);
        Task<PagedResult<WorkFlowReportLineDto>> GetReportLinesAsync(ReportQueryParams q);
        Task<(byte[] Content, string FileName, string ContentType)> ExportReportLinesAsync(ReportQueryParams q);
        Task<ResponseModel<PagedResult<WorkFlowBasicReportListDto>>> GetBasicWorkFlowReportAsync(WorkFlowBasicReportQueryParams q);
        Task<(byte[] Content, string FileName, string ContentType)> ExportBasicWorkFlowReportAsync(WorkFlowBasicReportQueryParams q);


        //Arşiv 

        Task<ResponseModel<PagedResult<WorkFlowArchiveListDto>>> GetArchiveListAsync(WorkFlowArchiveFilterDto filter);
        Task<ResponseModel<WorkFlowArchiveDetailDto>> GetArchiveDetailByIdAsync(long id);
        Task<ResponseModel<WorkFlowArchiveDetailDto>> GetArchiveDetailByRequestNoAsync(string requestNo);
         


        //Manitou System Test Zone ile ilgili işlemler eklenecek
        Task<ResponseModel<WorkingStatusDto>> StartWorking(StartWorkingDto dto);
        Task<ResponseModel<WorkingStatusDto>> GetWorkingStatus(string requestNo);
        Task<ResponseModel<WorkingStatusDto>> ExtendWorking(ExtendWorkingDto dto);
        Task<ResponseModel<FinishWorkingResultDto>> FinishWorking(FinishWorkingDto dto);

    }
}
