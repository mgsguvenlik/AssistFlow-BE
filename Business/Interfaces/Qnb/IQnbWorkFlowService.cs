using Core.Common;
using Core.Enums;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbCustomerForm;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbFinalApproval;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbPricing;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReport;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequest;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalService;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWarehouse;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlow;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlowStep;

namespace Business.Interfaces.Qnb
{
    public interface IQnbWorkFlowService
    {


        // -------------------- Customer Form / Services Request --------------------
        Task<ResponseModel<QnbCustomerFormGetDto>> CreateCustomerForm(QnbCustomerFormCreateDto dto);
        Task<ResponseModel<QnbCustomerFormGetDto>> GetCustomerFormByRequestNoAsync(string requestNo);

        //-------------------- Service Request Create--------------------
        Task<ResponseModel<QnbServicesRequestGetDto>> CreateRequestAsync(QnbServicesRequestCreateDto dto);

        Task<ResponseModel<PagedResult<QnbServicesRequestGetDto>>> GetRequestsAsync(QueryParams q);
        Task<ResponseModel<QnbServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo);
        Task<ResponseModel<QnbServicesRequestGetDto>> GetServiceRequestByIdAsync(long id);
        Task<ResponseModel<QnbServicesRequestGetDto>> UpdateServiceRequestAsync(QnbServicesRequestUpdateDto dto);
        Task<ResponseModel> DeleteRequestAsync(long id);

        // -------------------- Technical Service --------------------
        Task<ResponseModel<QnbTechnicalServiceGetDto>> SendTechnicalServiceAsync(QnbSendTechnicalServiceDto dto);
        Task<ResponseModel<QnbTechnicalServiceGetDto>> StartService(QnbStartTechnicalServiceDto dto);
        Task<ResponseModel<QnbTechnicalServiceGetDto>> FinishService(QnbFinishTechnicalServiceDto dto);
        Task<ResponseModel<QnbTechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo);

        // -------------------- Pricing --------------------
        Task<ResponseModel<QnbPricingGetDto>> ApprovePricing(QnbPricingUpdateDto dto);
        Task<ResponseModel<QnbPricingGetDto>> GetPricingByRequestNoAsync(string requestNo);

        // -------------------- Location override / Review --------------------
        Task<ResponseModel> RequestLocationOverrideAsync(QnbOverrideLocationCheckDto dto);
        Task<ResponseModel<QnbWorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes);
        Task<ResponseModel> SendReviewMessage(QnbCustomerReviewMessageDto dto);

        // -------------------- Final Approval --------------------
        Task<ResponseModel<QnbFinalApprovalGetDto>> FinalApprovalAsync(QnbFinalApprovalUpdateDto dto);
        Task<ResponseModel<QnbFinalApprovalGetDto>> GetFinalApprovalByRequestNoAsync(string requestNo);
        Task<ResponseModel<QnbFinalApprovalGetDto>> GetFinalApprovalByIdAsync(long id);
        Task<ResponseModel<QnbFinalApprovalGetDto>> CustomerAgreementAsync(QnbCustomerAgreementDto dto);
        Task<ResponseModel<QnbFinalApprovalGetDto>> GetCustomerAgreementByRequestNoAsync(string requestNo, FinalApprovalStatus status = FinalApprovalStatus.CustomerApproval);

        // -------------------- WorkFlow Steps --------------------
        Task<ResponseModel<PagedResult<QnbWorkFlowStepGetDto>>> GetStepsAsync(QueryParams q);
        Task<ResponseModel<QnbWorkFlowStepGetDto>> GetStepByIdAsync(long id);
        Task<ResponseModel<QnbWorkFlowStepGetDto>> CreateStepAsync(QnbWorkFlowStepCreateDto dto);
        Task<ResponseModel<QnbWorkFlowStepGetDto>> UpdateStepAsync(QnbWorkFlowStepUpdateDto dto);
        Task<ResponseModel> DeleteStepAsync(long id);

        // -------------------- WorkFlow --------------------
        Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "QNB");
        Task<ResponseModel<PagedResult<QnbWorkFlowGetDto>>> GetWorkFlowsAsync(QnbWorkFlowQueryParams q);
        Task<ResponseModel> DeleteWorkFlowAsync(long id);
        Task<ResponseModel> CancelWorkFlowAsync(long id);

        // -------------------- Warehouse --------------------
        Task<ResponseModel<QnbWarehouseGetDto>> SendWarehouseAsync(QnbSendWarehouseDto dto);
        Task<ResponseModel<QnbWarehouseGetDto>> GetWarehouseByIdAsync(long id);
        Task<ResponseModel<QnbWarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo);
        Task<ResponseModel<QnbWarehouseGetDto>> CompleteDeliveryAsync(QnbCompleteDeliveryDto dto);

        // -------------------- Report --------------------
        Task<ResponseModel<QnbWorkFlowReportDto>> GetReportAsync(string requestNo);
        Task<PagedResult<QnbWorkFlowReportListItemDto>> GetReportsAsync(QnbReportQueryParams q);
        Task<PagedResult<QnbWorkFlowReportLineDto>> GetReportLinesAsync(QnbReportQueryParams q);
        Task<(byte[] Content, string FileName, string ContentType)> ExportReportLinesAsync(QnbReportQueryParams q);
        Task<ResponseModel<PagedResult<QnbBasicReportListDto>>> GetQnbBasicWorkFlowReportAsync(QnbBasicReportQueryParams q);

        // -------------------- Archive --------------------
        Task<ResponseModel<PagedResult<QnbWorkFlowArchiveListDto>>> GetArchiveListAsync(QnbWorkFlowArchiveFilterDto filter);
        Task<ResponseModel<QnbWorkFlowArchiveDetailDto>> GetArchiveDetailByIdAsync(long id);
        Task<ResponseModel<QnbWorkFlowArchiveDetailDto>> GetArchiveDetailByRequestNoAsync(string requestNo);
    }
}