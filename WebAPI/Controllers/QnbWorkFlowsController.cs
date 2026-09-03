using Business.Interfaces;
using Business.Interfaces.Qnb;
using Business.Services.Qnb;
using Core.Common;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos;
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
using System.Net;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class QnbWorkFlowsController : ControllerBase
    {
        private readonly IQnbWorkFlowService _workFlowService;
        private readonly IActivationRecordService _activationRecordService;

        public QnbWorkFlowsController(IQnbWorkFlowService workFlowService, IActivationRecordService activationRecordService)
        {
            _workFlowService = workFlowService;
            _activationRecordService = activationRecordService;
        }

        [HttpGet("generate-request-no")]
        [MenuAuthorize("QnbServiceRequestCreate", MenuPermission.View)]
        public async Task<IActionResult> GetFlowRequestNo(string prfeix = "QNB")
        {
            var result = await _workFlowService.GetRequestNoAsync(prfeix);
            return Ok(result);
        }


        [HttpPost("create-services-request")]
        [MenuAuthorize("QnbServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> CreateRequest([FromBody] QnbServicesRequestCreateDto dto)
        {
            var result = await _workFlowService.CreateRequestAsync(dto);
            return Ok(result);
        }


        [HttpPost("send-warehouse")]
        [MenuAuthorize("QnbServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> SendWarehouse([FromBody] QnbSendWarehouseDto dto)
        {
            var result = await _workFlowService.SendWarehouseAsync(dto);
            return Ok(result);
        }

        [HttpPost("get-warehouse-byid")]
        [MenuAuthorize("QnbServiceRequestWarehouse", MenuPermission.View)]
        public async Task<IActionResult> GetWarehouseById([FromBody] long id)
        {
            var result = await _workFlowService.GetWarehouseByIdAsync(id);
            return Ok(result);
        }

        [HttpGet("get-warehouse-byrequestno")]
        [MenuAuthorize("QnbServiceRequestWarehouse", MenuPermission.View)]
        public async Task<IActionResult> GetWarehouseByRequestNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetWarehouseByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("complete-delivery")]
        [MenuAuthorize("QnbServiceRequestWarehouse", MenuPermission.Edit)]
        public async Task<IActionResult> CompleteDelivery([FromBody] QnbCompleteDeliveryDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Geçersiz veri gönderildi." });

            var result = await _workFlowService.CompleteDeliveryAsync(dto);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            return Ok(result);
        }

        [HttpGet("get-workflow-list")]
        [MenuAuthorize("QnbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetWorkFlowList([FromQuery] QnbWorkFlowQueryParams p)
        {
            var result = await _workFlowService.GetWorkFlowsAsync(p);
            return Ok(result);
        }

        [HttpPost("delete-workflow/{id:long}")]
        [MenuAuthorize(new[] { "QnbServiceRequestList", "QnbBasicWorkflowReportsList" }, MenuPermission.Edit)]
        public virtual async Task<IActionResult> DeleteWorkFlow([FromRoute] long id)
        {
            var result = await _workFlowService.DeleteWorkFlowAsync(id);
            return Ok(result);
        }

        [HttpPost("cancel-workflow/{id:long}")]
        [MenuAuthorize("QnbServiceRequestList", MenuPermission.Edit)]
        public async Task<IActionResult> CancelWorkFlow([FromRoute] long id)
        {
            var result = await _workFlowService.CancelWorkFlowAsync(id);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-byid/{id:long}")]
        [MenuAuthorize("QnbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetServicesRequesById([FromRoute] long id)
        {
            var result = await _workFlowService.GetServiceRequestByIdAsync(id);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-list")]
        [MenuAuthorize("QnbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetServicesRequestList([FromQuery] QueryParams p)
        {
            var result = await _workFlowService.GetRequestsAsync(p);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-byrequestno")]
        [MenuAuthorize(new[] { "QnbCustomerServiceRequestCreate", "QnbServiceRequestCreate", "QnbServiceRequestList", "QnbServiceRequestWarehouse", "QnbServiceRequestTechnicalService", "QnbServiceRequestPricing", "QnbServiceRequestFinalApproval", "QnbServiceRequestCustomerAgreement" }, MenuPermission.View)]
        public async Task<IActionResult> GetServicesRequestByNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetServiceRequestByRequestNoAsync(requestNo);
            return Ok(result);
        }



        [HttpPost("update-services-request/{id:long}")]
        [MenuAuthorize("QnbServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> UpdateServicesRequest([FromRoute] long id, [FromBody] QnbServicesRequestUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateServiceRequestAsync(dto);
            return ToActionResult(resp);
        }

        [HttpGet("get-technicalservice-by-requestno")]
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.View)]
        public async Task<IActionResult> GetTechnicalServiceByRequestNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetTechnicalServiceByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("delete-technical-service/image/{id}")]
        [MenuAuthorize("QnbServiceRequestFinalApproval", MenuPermission.Edit)]
        public async Task<IActionResult> DeleteTechnicalServiceImage(long id, TechnicalServiceImageType type, CancellationToken cancellationToken)
        {
            var result = await _workFlowService.DeleteTechnicalServiceImageAsync(id, type, cancellationToken);
            return Ok(result);
        }

        [HttpPost("send-technical-service")]
        [MenuAuthorize("QnbServiceRequestCreate", MenuPermission.Edit)]
        public async Task<IActionResult> SendTechnicalServiceAsync([FromBody] QnbSendTechnicalServiceDto dto)
        {
            var result = await _workFlowService.SendTechnicalServiceAsync(dto);
            return Ok(result);
        }

        [HttpPost("start-technical-service")]
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> StartTechnicalServiceAsync([FromBody] QnbStartTechnicalServiceDto dto)
        {
            var result = await _workFlowService.StartService(dto);
            return Ok(result);
        }

        [HttpPost("finish-technical-service")]
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000, ValueCountLimit = 2048)]
        public async Task<IActionResult> FinishTechnicalServiceAsync([FromForm] QnbFinishTechnicalServiceDto dto)
        {
            var result = await _workFlowService.FinishService(dto);
            return Ok(result);
        }

        [HttpPost("approve-pricing")]
        [MenuAuthorize("QnbServiceRequestPricing", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(275_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 275_000_000, ValueCountLimit = 2048)]
        public async Task<IActionResult> ApprovePricing([FromForm] QnbPricingUpdateDto dto)
        {
            var result = await _workFlowService.ApprovePricing(dto);
            return Ok(result);
        }

        [HttpGet("get-pricing-by-requestno")]
        [MenuAuthorize("QnbServiceRequestPricing", MenuPermission.View)]
        public async Task<IActionResult> GetPricingByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetPricingByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("final-approve")]
        [MenuAuthorize("QnbServiceRequestFinalApproval", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(275_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 275_000_000, ValueCountLimit = 2048)]
        public async Task<IActionResult> FinalApprove([FromForm] QnbFinalApprovalUpdateDto dto)
        {
            var result = await _workFlowService.FinalApprovalAsync(dto);
            return Ok(result);
        }

        [HttpGet("get-finalapproval-by-requestno")]
        [MenuAuthorize("QnbServiceRequestFinalApproval", MenuPermission.View)]
        public async Task<IActionResult> GetFinalApprovalByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetFinalApprovalByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpGet("get-finalapproval-by-id")]
        [MenuAuthorize("QnbServiceRequestFinalApproval", MenuPermission.View)]
        public async Task<IActionResult> GetFinalApprovalByIdAsync([FromQuery] long id)
        {
            var result = await _workFlowService.GetFinalApprovalByIdAsync(id);
            return Ok(result);
        }

        [HttpPost("location-override")]
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> RequestLocationOverrideAsync([FromBody] QnbOverrideLocationCheckDto dto)
        {
            var result = await _workFlowService.RequestLocationOverrideAsync(dto);
            return Ok(result);
        }

        [HttpPost("send-back-for-review")]
        [MenuAuthorize(new[] { "QnbServiceRequestWarehouse", "QnbServiceRequestTechnicalService", "QnbServiceRequestFinalApproval", "QnbServiceRequestCustomerAgreement" }, MenuPermission.Edit)]
        public async Task<IActionResult> SendBackForReviewAsync([FromQuery] string requestNo, [FromQuery] string reviewNotes)
        {
            var result = await _workFlowService.SendBackForReviewAsync(requestNo, reviewNotes);
            return Ok(result);
        }


        [HttpGet("activity-records/{requestNo}")]
        [MenuAuthorize("QnbServiceRequestList", MenuPermission.View)]
        public async Task<IActionResult> GetLatestActivityRecords([FromRoute] string requestNo)
        {
            var result = await _activationRecordService.GetLatestQnbActivityRecordByRequestNoAsync(requestNo);
            return ToActionResult(result);
        }

        // ---------- WorkFlowStep CRUD ----------
        [HttpGet("get-workflow-steps")]
        [MenuAuthorize(new[] { "QnbFlowStatusList", "QnbCustomerServiceRequestCreate", "QnbServiceRequestCreate", "QnbServiceRequestList", "QnbServiceRequestWarehouse", "QnbServiceRequestTechnicalService", "QnbServiceRequestPricing", "QnbServiceRequestFinalApproval", "QnbServiceRequestCustomerAgreement", "QnbTechnicianDashboard" }, MenuPermission.View)]
        public async Task<IActionResult> GetSteps([FromQuery] QueryParams q)
        {
            var resp = await _workFlowService.GetStepsAsync(q);
            return ToActionResult(resp);
        }

        [HttpGet("get-workflow-steps/{id:long}")]
        [MenuAuthorize(new[] { "QnbFlowStatusList", "QnbCustomerServiceRequestCreate", "QnbServiceRequestCreate", "QnbServiceRequestList", "QnbServiceRequestWarehouse", "QnbServiceRequestTechnicalService", "QnbServiceRequestPricing", "QnbServiceRequestFinalApproval", "QnbServiceRequestCustomerAgreement", "QnbTechnicianDashboard" }, MenuPermission.View)]
        public async Task<IActionResult> GetStepsById([FromRoute] long id)
        {
            var resp = await _workFlowService.GetStepByIdAsync(id);
            return ToActionResult(resp);
        }

        [HttpPost("create-steps")]
        [MenuAuthorize("QnbFlowStatusList", MenuPermission.Edit)]
        public async Task<IActionResult> CreateSteps([FromBody] QnbWorkFlowStepCreateDto dto)
        {
            var resp = await _workFlowService.CreateStepAsync(dto);

            if (resp.IsSuccess && resp.Data is not null)
                return CreatedAtAction(nameof(GetStepsById), new { id = resp.Data.Id }, resp);

            return ToActionResult(resp);
        }

        [HttpPost("update-steps/{id:long}")]
        [MenuAuthorize("QnbFlowStatusList", MenuPermission.Edit)]
        public async Task<IActionResult> UpdateSteps([FromRoute] long id, [FromBody] QnbWorkFlowStepUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateStepAsync(dto);
            return ToActionResult(resp);
        }

        [HttpPost("delete-steps/{id:long}")]
        [MenuAuthorize("QnbFlowStatusList", MenuPermission.Edit)]
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
        [HttpGet("workflow-report")]
        [MenuAuthorize("QnbServiceReportsList", MenuPermission.View)]
        [ProducesResponseType(typeof(PagedResult<QnbWorkFlowReportListItemDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get([FromQuery] QnbReportQueryParams q, CancellationToken ct)
        {
            q.Normalize(maxPageSize: 500);

            var result = await _workFlowService.GetReportLinesAsync(q);
            return Ok(result);
        }

        [HttpGet("report-lines/export")]
        [MenuAuthorize("QnbServiceReportsList", MenuPermission.View)]
        public async Task<IActionResult> ExportReportLines([FromQuery] QnbReportQueryParams q)
        {
            var (content, fileName, contentType) = await _workFlowService.ExportReportLinesAsync(q);
            return File(content, contentType, fileName);
        }


        [HttpGet("basic-report")]
        [MenuAuthorize("QnbBasicWorkflowReportsList", MenuPermission.View)]
        public async Task<IActionResult> GetBasicReport([FromQuery] QnbBasicReportQueryParams q)
        {
            var result = await _workFlowService.GetQnbBasicWorkFlowReportAsync(q);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("basic-report/export")]
        [MenuAuthorize("QnbBasicWorkflowReportsList", MenuPermission.View)]
        public async Task<IActionResult> ExportQnbBasicReport([FromQuery] QnbBasicReportQueryParams q)
        {
            var (content, fileName, contentType) = await _workFlowService.ExportQnbBasicWorkFlowReportAsync(q);
            return File(content, contentType, fileName);
        }
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
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> StartWorking([FromBody] StartWorkingDto dto)
        {
            var result = await _workFlowService.StartWorking(dto);
            return ToActionResult(result);
        }

        [HttpGet("working-status")]
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.View)]
        public async Task<IActionResult> GetWorkingStatus([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetWorkingStatus(requestNo);
            return ToActionResult(result);
        }

        [HttpPost("extend-working")]
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> ExtendWorking([FromBody] ExtendWorkingDto dto)
        {
            var result = await _workFlowService.ExtendWorking(dto);
            return ToActionResult(result);
        }

        [HttpPost("finish-working")]
        [MenuAuthorize("QnbServiceRequestTechnicalService", MenuPermission.Edit)]
        public async Task<IActionResult> FinishWorking([FromBody] FinishWorkingDto dto)
        {
            var result = await _workFlowService.FinishWorking(dto);
            return ToActionResult(result);
        }



    }
}
