using Core.Common;
using Core.Enums;
using Microsoft.AspNetCore.Http;
using Model.Concrete.Ekb;
using Model.Dtos.WorkFlowDtos;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAccounting;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbArchive;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAttachment;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbCustomerForm;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbFinalApproval;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbPricing;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbReport;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequest;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalService;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbWarehouse;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbWorkFlow;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbWorkFlowStep;
using System.Threading.Tasks;

namespace Business.Interfaces.Ekb
{
    public interface IEkbWorkFlowService
    {
        Task<ResponseModel<EkbCustomerFormGetDto>> CreateCustomerForm(EkbCustomerFormCreateDto dto);
        Task<ResponseModel<PagedResult<EkbServicesRequestGetDto>>> GetRequestsAsync(QueryParams q);
        Task<ResponseModel<PagedResult<ActiveCustomerRequestDto>>> GetActiveCustomerRequestsAsync(long customerId, int page, int pageSize);
        Task<ResponseModel<EkbServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo);

        Task<ResponseModel<EkbServicesRequestGetDto>> GetServiceRequestByIdAsync(long id);
        Task<ResponseModel<EkbServicesRequestGetDto>> UpdateServiceRequestAsync(EkbServicesRequestUpdateDto dto);
        Task<ResponseModel> DeleteRequestAsync(long id);
        Task<ResponseModel<EkbTechnicalServiceGetDto>> SendTechnicalServiceAsync(EkbSendTechnicalServiceDto dto);
        Task<ResponseModel<EkbTechnicalServiceGetDto>> StartService(EkbStartTechnicalServiceDto dto);
        Task<ResponseModel<EkbTechnicalServiceGetDto>> FinishService(EkbFinishTechnicalServiceDto dto);
        Task<ResponseModel<EkbPricingGetDto>> ApprovePricing(EkbPricingUpdateDto dto);
        Task<ResponseModel<EkbPricingGetDto>> GetPricingByRequestNoAsync(string requestNo);
        Task<ResponseModel> RequestLocationOverrideAsync(EkbOverrideLocationCheckDto dto);
        Task<ResponseModel<EkbWorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes);
        Task<ResponseModel> SendReviewMessage(EkbCustomerReviewMessageDto dto);

        Task<ResponseModel<EkbFinalApprovalGetDto>> FinalApprovalAsync(EkbFinalApprovalUpdateDto dto);
        Task<ResponseModel<EkbFinalApprovalGetDto>> GetFinalApprovalByRequestNoAsync(string requestNo);
        Task<ResponseModel<EkbFinalApprovalGetDto>> GetFinalApprovalByIdAsync(long id);
        Task<ResponseModel<EkbFinalApprovalGetDto>> CustomerAgreementAsync(EkbCustomerAgreementDto dto);


        Task<ResponseModel<EkbCustomerFormGetDto>> GetCustomerFormByRequestNoAsync(string requestNo);
        // WorkFlowStep
        Task<ResponseModel<PagedResult<EkbWorkFlowStepGetDto>>> GetStepsAsync(QueryParams q);
        Task<ResponseModel<EkbWorkFlowStepGetDto>> GetStepByIdAsync(long id);
        Task<ResponseModel<EkbWorkFlowStepGetDto>> CreateStepAsync(EkbWorkFlowStepCreateDto dto);
        Task<ResponseModel<EkbWorkFlowStepGetDto>> UpdateStepAsync(EkbWorkFlowStepUpdateDto dto);
        Task<ResponseModel> DeleteStepAsync(long id);

        // WorkFlow (tanım)

        Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "SR");
        Task<ResponseModel<PagedResult<EkbWorkFlowGetDto>>> GetWorkFlowsAsync(EkbWorkFlowQueryParams q);
        Task<ResponseModel> DeleteWorkFlowAsync(long id);
        Task<ResponseModel> CancelWorkFlowAsync(long id);

        // Warehouse (depo) ile ilgili işlemler 
        Task<ResponseModel<EkbWarehouseGetDto>> SendWarehouseAsync(EkbSendWarehouseDto dto);
        Task<ResponseModel<EkbWarehouseGetDto>> GetWarehouseByIdAsync(long id);
        Task<ResponseModel<EkbWarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo);
        Task<ResponseModel<EkbWarehouseGetDto>> CompleteDeliveryAsync(EkbCompleteDeliveryDto dto);

        //Teknik Servis ile ilgili işlemler eklenecek
        Task<ResponseModel<EkbTechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo);
        Task<ResponseModel> DeleteTechnicalServiceImageAsync(long id, TechnicalServiceImageType type, CancellationToken cancellationToken = default);

        // Müşteri Onayı 
        Task<ResponseModel<EkbFinalApprovalGetDto>> GetCustomerAgreementByRequestNoAsync(string requestNo, FinalApprovalStatus staqtus = FinalApprovalStatus.CustomerApproval);

        // Report 

        //Task<ResponseModel<PagedResult<WorkFlowReportListItemDto>>> GetReportsAsync(ReportQueryParams q);
        Task<ResponseModel<EkbWorkFlowReportDto>> GetReportAsync(string requestNo);

        Task<PagedResult<EkbWorkFlowReportListItemDto>> GetReportsAsync(EkbReportQueryParams q);
        Task<PagedResult<EkbWorkFlowReportLineDto>> GetReportLinesAsync(EkbReportQueryParams q);

        Task<(byte[] Content, string FileName, string ContentType)> ExportReportLinesAsync(EkbReportQueryParams q);

        Task<ResponseModel<PagedResult<EkbBasicReportListDto>>> GetEkbBasicWorkFlowReportAsync(EkbBasicReportQueryParams q);
        Task<(byte[] Content, string FileName, string ContentType)> ExportEkbBasicWorkFlowReportAsync(EkbBasicReportQueryParams q);

        //Arşiv 
        Task<ResponseModel<PagedResult<EkbWorkFlowArchiveListDto>>> GetArchiveListAsync(EkbWorkFlowArchiveFilterDto filter);
        Task<ResponseModel<EkbWorkFlowArchiveDetailDto>> GetArchiveDetailByIdAsync(long id);
        Task<ResponseModel<EkbWorkFlowArchiveDetailDto>> GetArchiveDetailByRequestNoAsync(string requestNo);


        //Manitou System Test Zone ile ilgili işlemler eklenecek
        Task<ResponseModel<WorkingStatusDto>> StartWorking(StartWorkingDto dto);
        Task<ResponseModel<WorkingStatusDto>> GetWorkingStatus(string requestNo);
        Task<ResponseModel<WorkingStatusDto>> ExtendWorking(ExtendWorkingDto dto);
        Task<ResponseModel<FinishWorkingResultDto>> FinishWorking(FinishWorkingDto dto);


        //Muhasebe ile ilgili işlemler
        Task<ResponseModel<PagedResult<EkbAccountingServiceReportDto>>> GetAccountingServiceReportAsync(EkbAccountingReportQueryParams q);
        Task<ResponseModel<EkbAccountingStatusDto>> ToggleAccountingProcessAsync(string requestNo);

        Task<ResponseModel<List<EkbWorkflowAttachmentGetDto>>> AddAccountingAttachmentsAsync(string requestNo, IReadOnlyCollection<IFormFile>? files, CancellationToken cancellationToken = default);
        Task<ResponseModel<List<EkbWorkflowAttachmentGetDto>>> GetAccountingAttachmentsAsync(string requestNo, CancellationToken cancellationToken = default);

        Task<ResponseModel<List<EkbWorkflowAttachmentGetDto>>> DeleteAccountingAttachmentAsync(string requestNo, long attachmentId, CancellationToken cancellationToken = default);

       
    }
}
