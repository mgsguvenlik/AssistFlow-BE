using Business.Interfaces;
using Business.Interfaces.Ekb;
using Business.Services.Ekb;
using Core.Common;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAccounting;
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
using System.Net;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EkbWorkFlowsController : ControllerBase
    {
        private readonly IEkbWorkFlowService _workFlowService;
        private readonly IActivationRecordService _activationRecordService;
        public EkbWorkFlowsController(IEkbWorkFlowService workFlowService, IActivationRecordService activationRecordService)
        {
            _workFlowService = workFlowService;
            _activationRecordService = activationRecordService;
        }

        [HttpGet("generate-request-no")]
        [MenuAuthorize("EkbServiceRequestCreate", MenuPermission.View)]
        public async Task<IActionResult> GetFlowRequestNo(string prfeix = "EKB")
        {
            var result = await _workFlowService.GetRequestNoAsync(prfeix);
            return Ok(result);
        }
        [HttpPost("create-customer-form")]
        [MenuAuthorize("EkbCustomerServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> CreateCustomerForm([FromBody] EkbCustomerFormCreateDto dto)
        {
            var result = await _workFlowService.CreateCustomerForm(dto);
            return Ok(result);
        }

        [HttpPost("send-warehouse")]
        [MenuAuthorize("EkbServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> SendWarehouse([FromBody] EkbSendWarehouseDto dto)
        {
            var result = await _workFlowService.SendWarehouseAsync(dto);
            return Ok(result);
        }

        [HttpPost("get-warehouse-byid")]
        [MenuAuthorize("EkbServiceRequestWarehouse", MenuPermission.View)]
        public async Task<IActionResult> GetWarehouseById([FromBody] long id)
        {
            var result = await _workFlowService.GetWarehouseByIdAsync(id);
            return Ok(result);
        }
        [HttpGet("get-warehouse-byrequestno")]
        [MenuAuthorize("EkbServiceRequestWarehouse", MenuPermission.View)]
        public async Task<IActionResult> GetWarehouseByRequestNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetWarehouseByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("complete-delivery")]
        [MenuAuthorize("EkbServiceRequestWarehouse", MenuPermission.Edit)]
        public async Task<IActionResult> CompleteDelivery([FromBody] EkbCompleteDeliveryDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Geçersiz veri gönderildi." });
            var result = await _workFlowService.CompleteDeliveryAsync(dto);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            return Ok(result);
        }

        [HttpGet("get-workflow-list")]
        [MenuAuthorize("EkbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetWorkFlowList([FromQuery] EkbWorkFlowQueryParams p)
        {
            var result = await _workFlowService.GetWorkFlowsAsync(p);
            return Ok(result);
        }

        [HttpGet("active-customer-requests")]
        [MenuAuthorize(new[] { "EkbCustomerServiceRequestCreate", "EkbServiceRequestCreate" }, MenuPermission.View)]
        public async Task<IActionResult> GetActiveCustomerRequests([FromQuery] long customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _workFlowService.GetActiveCustomerRequestsAsync(customerId, page, pageSize);
            return Ok(result);
        }

        [HttpPost("delete-workflow/{id:long}")]
        [MenuAuthorize(new[] { "EkbServiceRequestList", "EkbBasicWorkflowReportsList", "EkbAssignedServiceRequestDeleteUpdate" }, MenuPermission.Edit)]
        public virtual async Task<IActionResult> DeleteWorkFlow([FromRoute] long id)
        {
            var result = await _workFlowService.DeleteWorkFlowAsync(id);
            return Ok(result);
        }

        [HttpPost("cancel-workflow/{id:long}")]
        [MenuAuthorize(new[] { "EkbServiceRequestList", "EkbAssignedServiceRequestDeleteUpdate" }, MenuPermission.Edit)]
        public async Task<IActionResult> CancelWorkFlow([FromRoute] long id)
        {
            var result = await _workFlowService.CancelWorkFlowAsync(id);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-byid/{id:long}")]
        [MenuAuthorize("EkbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetServicesRequesById([FromRoute] long id)
        {
            var result = await _workFlowService.GetServiceRequestByIdAsync(id);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-list")]
        [MenuAuthorize("EkbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetServicesRequestList([FromQuery] QueryParams p)
        {
            var result = await _workFlowService.GetRequestsAsync(p);
            return Ok(result);
        }


        [HttpGet("get-servicesrequest-byrequestno")]
        [MenuAuthorize(new[] { "EkbCustomerServiceRequestCreate", "EkbServiceRequestCreate", "EkbServiceRequestList", "EkbServiceRequestWarehouse", "EkbServiceRequestTechnicalService", "EkbServiceRequestPricing", "EkbServiceRequestFinalApproval", "EkbServiceRequestCustomerAgreement" }, MenuPermission.View)]
        public async Task<IActionResult> GetServicesRequestByNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetServiceRequestByRequestNoAsync(requestNo);
            return Ok(result);
        }
        [HttpGet("get-customerform-byrequestno")]
        [MenuAuthorize("EkbCustomerServiceRequestCreate", MenuPermission.View)]
        public async Task<IActionResult> GetCustomerFormByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetCustomerFormByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("update-services-request/{id:long}")]
        [MenuAuthorize("EkbServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> UpdateServicesRequest([FromRoute] long id, [FromBody] EkbServicesRequestUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateServiceRequestAsync(dto);
            return ToActionResult(resp);
        }

        [HttpGet("get-technicalservice-by-requestno")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.View)]
        public async Task<IActionResult> GetTechnicalServiceByRequestNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetTechnicalServiceByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("send-technical-service")]
        [MenuAuthorize("EkbServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> SendTechnicalServiceAsync([FromBody] EkbSendTechnicalServiceDto dto)
        {
            var result = await _workFlowService.SendTechnicalServiceAsync(dto);
            return Ok(result);
        }


        [HttpPost("start-technical-service")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> StartTechnicalServiceAsync([FromBody] EkbStartTechnicalServiceDto dto)
        {
            var result = await _workFlowService.StartService(dto);
            return Ok(result);
        }

        [HttpPost("finish-technical-service")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)] // 200 MB örnek
        [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000, ValueCountLimit = 2048)]
        public async Task<IActionResult> FinishTechnicalServiceAsync([FromForm] EkbFinishTechnicalServiceDto dto)
        {
            var result = await _workFlowService.FinishService(dto);
            return Ok(result);
        }


        [HttpPost("delete-technical-service/image/{id}")]
        [MenuAuthorize("EkbServiceRequestFinalApproval", MenuPermission.Edit)]
        public async Task<IActionResult> DeleteTechnicalServiceImage(long id, TechnicalServiceImageType type, CancellationToken cancellationToken)
        {
            var result = await _workFlowService.DeleteTechnicalServiceImageAsync(id, type, cancellationToken);
            return Ok(result);
        }

        [HttpPost("approve-pricing")]
        [MenuAuthorize("EkbServiceRequestPricing", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(275_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 275_000_000)]
        public async Task<IActionResult> ApprovePricingAsync([FromForm] EkbPricingUpdateDto dto)
        {
            var result = await _workFlowService.ApprovePricing(dto);
            return Ok(result);
        }

        [HttpGet("get-pricing-by-requestno")]
        [MenuAuthorize("EkbServiceRequestPricing", MenuPermission.View)]
        public async Task<IActionResult> GetPricingByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetPricingByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("final-approve")]
        [MenuAuthorize("EkbServiceRequestFinalApproval", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(275_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 275_000_000)]
        public async Task<IActionResult> FinalApprove(
            [FromForm] EkbFinalApprovalUpdateDto dto)
        {
            var result = await _workFlowService.FinalApprovalAsync(dto);
            return Ok(result);
        }

        [HttpPost("customer-agreement")]
        [MenuAuthorize("EkbServiceRequestCustomerAgreement", MenuPermission.Edit)]
        public async Task<IActionResult> CustomerAgreementAsync([FromBody] EkbCustomerAgreementDto dto)
        {
            var result = await _workFlowService.CustomerAgreementAsync(dto);
            return Ok(result);
        }




        [HttpGet("get-finalapproval-by-requestno")]
        [MenuAuthorize("EkbServiceRequestFinalApproval", MenuPermission.View)]
        public async Task<IActionResult> GetFinalApprovalByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetFinalApprovalByRequestNoAsync(requestNo);
            return Ok(result);
        }



        [HttpGet("get-customeragreement-by-requestno")]
        [MenuAuthorize("EkbServiceRequestCustomerAgreement", MenuPermission.View)]
        public async Task<IActionResult> GetCustomerAgreementByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetCustomerAgreementByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpGet("get-finalapproval-by-id")]
        [MenuAuthorize("EkbServiceRequestFinalApproval", MenuPermission.View)]
        public async Task<IActionResult> GetFinalApprovalByIdAsync([FromQuery] long id)
        {
            var result = await _workFlowService.GetFinalApprovalByIdAsync(id);
            return Ok(result);
        }

        [HttpPost("location-override")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> RequestLocationOverrideAsync([FromBody] EkbOverrideLocationCheckDto dto)
        {
            var result = await _workFlowService.RequestLocationOverrideAsync(dto);
            return Ok(result);
        }


        [HttpPost("send-back-for-review")]
        [MenuAuthorize(new[] { "EkbServiceRequestWarehouse", "EkbServiceRequestTechnicalService", "EkbServiceRequestFinalApproval", "EkbServiceRequestCustomerAgreement" }, MenuPermission.Edit)]
        public async Task<IActionResult> SendBackForReviewAsync([FromQuery] string requestNo, [FromQuery] string reviewNotes)
        {
            var result = await _workFlowService.SendBackForReviewAsync(requestNo, reviewNotes);
            return Ok(result);
        }

        [HttpPost("send-review-message")]
        [MenuAuthorize("EkbServiceRequestFinalApproval", MenuPermission.Edit)]
        public async Task<IActionResult> SendReviewMessage([FromBody] EkbCustomerReviewMessageDto dto)
        {
            var result = await _workFlowService.SendReviewMessage(dto);
            return Ok(result);
        }

        [HttpGet("activity-records/{requestNo}")]
        [MenuAuthorize("EkbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetLatestActivityRecords([FromRoute] string requestNo)
        {
            var result = await _activationRecordService.GetLatestEkbActivityRecordByRequestNoAsync(requestNo);
            return ToActionResult(result);
        }


        // ---------- WorkFlowStep CRUD ----------
        // GET: /api/workflows/steps
        [HttpGet("get-workflow-steps")]
        [MenuAuthorize(new[] { "EkbFlowStatusList", "EkbCustomerServiceRequestCreate", "EkbServiceRequestCreate", "EkbServiceRequestList", "EkbServiceRequestWarehouse", "EkbServiceRequestTechnicalService", "EkbServiceRequestPricing", "EkbServiceRequestFinalApproval", "EkbServiceRequestCustomerAgreement", "EkbTechnicianDashboard" }, MenuPermission.View)]
        public async Task<IActionResult> GetSteps([FromQuery] QueryParams q)
        {
            var resp = await _workFlowService.GetStepsAsync(q);
            return ToActionResult(resp);
        }

        [HttpGet("get-workflow-steps/{id:long}")]
        [MenuAuthorize(new[] { "EkbFlowStatusList", "EkbCustomerServiceRequestCreate", "EkbServiceRequestCreate", "EkbServiceRequestList", "EkbServiceRequestWarehouse", "EkbServiceRequestTechnicalService", "EkbServiceRequestPricing", "EkbServiceRequestFinalApproval", "EkbServiceRequestCustomerAgreement", "EkbTechnicianDashboard" }, MenuPermission.View)]
        public async Task<IActionResult> GetStepsById([FromRoute] long id)
        {
            var resp = await _workFlowService.GetStepByIdAsync(id);
            return ToActionResult(resp);
        }

        [HttpPost("create-steps")]
        [MenuAuthorize("EkbFlowStatusList", MenuPermission.Edit)]
        public async Task<IActionResult> CreateSteps([FromBody] EkbWorkFlowStepCreateDto dto)
        {
            var resp = await _workFlowService.CreateStepAsync(dto);

            if (resp.IsSuccess && resp.Data is not null)
                return CreatedAtAction(nameof(GetStepsById), new { id = resp.Data.Id }, resp);

            return ToActionResult(resp);
        }

        [HttpPost("update-steps/{id:long}")]
        [MenuAuthorize("EkbFlowStatusList", MenuPermission.Edit)]
        public async Task<IActionResult> UpdateSteps([FromRoute] long id, [FromBody] EkbWorkFlowStepUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateStepAsync(dto);
            return ToActionResult(resp);
        }

        [HttpPost("delete-steps/{id:long}")]
        [MenuAuthorize("EkbFlowStatusList", MenuPermission.Edit)]
        public async Task<IActionResult> DeleteSteps([FromRoute] long id)
        {
            var resp = await _workFlowService.DeleteStepAsync(id);
            if (resp.IsSuccess && resp.StatusCode == Core.Enums.StatusCode.Ok)
                return NoContent();

            return ToActionResult(resp);
        }


        // ----------- Report ------------
        /// <summary>
        /// Çoklu filtreli rapor arama (paging + sort).
        /// </summary>
        /// <remarks>
        /// Örnek:
        /// GET /api/reports?Page=1&PageSize=20&CreatedFrom=2025-11-01&CreatedTo=2025-11-10&RequestNo=SR-20251108
        /// &WorkFlowStatuses=Pending&WorkFlowStatuses=Complated&TechnicianId=12&ProductCode=ABC
        /// </remarks>
        [HttpGet("workflow-report")]
        [MenuAuthorize("EkbServiceReportsList", MenuPermission.View)]
        [ProducesResponseType(typeof(PagedResult<EkbWorkFlowReportListItemDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get([FromQuery] EkbReportQueryParams q, CancellationToken ct)
        {
            q.Normalize(maxPageSize: 500);

            var result = await _workFlowService.GetReportLinesAsync(q);
            return Ok(result);
        }

        [HttpGet("report-lines/export")]
        [MenuAuthorize("EkbServiceReportsList", MenuPermission.View)]
        public async Task<IActionResult> ExportReportLines([FromQuery] EkbReportQueryParams q)
        {
            var (content, fileName, contentType) = await _workFlowService.ExportReportLinesAsync(q);
            return File(content, contentType, fileName);
        }

        [HttpGet("basic-report")]
        [MenuAuthorize("EkbBasicWorkflowReportsList", MenuPermission.View)]
        public async Task<IActionResult> GetBasicReport([FromQuery] EkbBasicReportQueryParams q)
        {
            var result = await _workFlowService.GetEkbBasicWorkFlowReportAsync(q);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("basic-report/export")]
        [MenuAuthorize("EkbBasicWorkflowReportsList", MenuPermission.View)]
        public async Task<IActionResult> ExportBasicReport([FromQuery] EkbBasicReportQueryParams q)
        {
            var (content, fileName, contentType) = await _workFlowService.ExportEkbBasicWorkFlowReportAsync(q);

            return File(content, contentType, fileName);
        }


        //---------Arşiv---------------


        // ---------- Helpers ----------
        private IActionResult ToActionResult(ResponseModel resp)
        {
            if (resp.StatusCode == Core.Enums.StatusCode.NoContent)
                return StatusCode((int)Core.Enums.StatusCode.NoContent);

            return StatusCode((int)resp.StatusCode, resp);
        }

        private IActionResult ToActionResult<T>(ResponseModel<T> resp)
        {
            if (resp.StatusCode == Core.Enums.StatusCode.NoContent)
                return StatusCode((int)Core.Enums.StatusCode.NoContent);

            return StatusCode((int)resp.StatusCode, resp);
        }


        ///Manitou System Test Zone ilgili işlemler

        [HttpPost("start-working")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> StartWorking([FromBody] StartWorkingDto dto)
        {
            var result = await _workFlowService.StartWorking(dto);
            return ToActionResult(result);
        }

        [HttpGet("working-status")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.View)]
        public async Task<IActionResult> GetWorkingStatus([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetWorkingStatus(requestNo);
            return ToActionResult(result);
        }

        [HttpPost("extend-working")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> ExtendWorking([FromBody] ExtendWorkingDto dto)
        {
            var result = await _workFlowService.ExtendWorking(dto);
            return ToActionResult(result);
        }

        [HttpPost("finish-working")]
        [MenuAuthorize("EkbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> FinishWorking([FromBody] FinishWorkingDto dto)
        {
            var result = await _workFlowService.FinishWorking(dto);
            return ToActionResult(result);
        }


        // ----------- Muhasebe ile ilgili işlemler ------------

        [HttpGet("accounting-service-report")]
        [MenuAuthorize("EkbAccountingServiceReportList", MenuPermission.View)]
        public async Task<IActionResult> GetAccountingServiceReport(
            [FromQuery] EkbAccountingReportQueryParams q)
        {
            var result = await _workFlowService
                .GetAccountingServiceReportAsync(q);

            return ToActionResult(result);
        }


        [HttpPost("accounting-service-report/{requestNo}/toggle")]
        [MenuAuthorize("EkbAccountingServiceReportList", MenuPermission.Edit)]
        public async Task<IActionResult> ToggleAccountingProcess(
            [FromRoute] string requestNo)
        {
            var result = await _workFlowService
                .ToggleAccountingProcessAsync(requestNo);

            return ToActionResult(result);
        }


        [HttpPost("accounting/attachments")]
        [MenuAuthorize("EkbAccountingServiceReportList", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        public async Task<ResponseModel<List<EkbWorkflowAttachmentGetDto>>> AddAccountingAttachments([FromForm] string requestNo, [FromForm] List<IFormFile> files, CancellationToken cancellationToken)
        {
            return await _workFlowService
                .AddAccountingAttachmentsAsync(
                    requestNo,
                    files,
                    cancellationToken);
        }

        [HttpGet("accounting/{requestNo}/attachments")]
        [MenuAuthorize("EkbAccountingServiceReportList", MenuPermission.View)]
        public async Task<ResponseModel<List<EkbWorkflowAttachmentGetDto>>> GetAccountingAttachments(string requestNo, CancellationToken cancellationToken)
        {
            return await _workFlowService
                .GetAccountingAttachmentsAsync(
                    requestNo,
                    cancellationToken);
        }

        [HttpPost("accounting/{requestNo}/attachments/{attachmentId:long}/delete")]
        [MenuAuthorize("EkbAccountingServiceReportList", MenuPermission.Edit)]
        public async Task<ResponseModel<List<EkbWorkflowAttachmentGetDto>>> DeleteAccountingAttachment(string requestNo, long attachmentId, CancellationToken cancellationToken)
        {
            return await _workFlowService.DeleteAccountingAttachmentAsync(
                requestNo,
                attachmentId,
                cancellationToken);
        }

    }
}
