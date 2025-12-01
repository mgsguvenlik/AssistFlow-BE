using Business.Interfaces;
using Business.Interfaces.Ykb;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbFinalApproval;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbPricing;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequest;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWarehouse;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlowStep;
using System.Net;

namespace WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class YkbWorkFlowsController : ControllerBase
    {
        private readonly IYkbWorkFlowService _workFlowService;
        private readonly IActivationRecordService _activationRecordService;
        public YkbWorkFlowsController(IYkbWorkFlowService workFlowService, IActivationRecordService activationRecordService)
        {
            _workFlowService = workFlowService;
            _activationRecordService = activationRecordService;
        }

        [HttpGet("generate-request-no")]
        public async Task<IActionResult> GetFlowRequestNo(string prfeix = "YKB")
        {
            var result = await _workFlowService.GetRequestNoAsync(prfeix);
            return Ok(result);
        }
        [HttpPost("create-customer-form")]
        public async Task<IActionResult> CreateCustomerForm([FromBody] YkbCustomerFormCreateDto dto)
        {
            var result = await _workFlowService.CreateCustomerForm(dto);
            return Ok(result);
        }

        [HttpPost("send-customer-form")]
        public async Task<IActionResult> SendCustomerForm([FromBody] YkbCustomerFormSendDto dto)
        {
            var result = await _workFlowService.SendCustomerFormToService(dto);
            return Ok(result);
        }


        [HttpPost("create-services-request")]
        public async Task<IActionResult> CreateRequest([FromBody] YkbServicesRequestCreateDto dto)
        {
            var result = await _workFlowService.CreateRequestAsync(dto);
            return Ok(result);
        }

        [HttpPost("send-warehouse")]
        public async Task<IActionResult> SendWarehouse([FromBody] YkbSendWarehouseDto dto)
        {
            var result = await _workFlowService.SendWarehouseAsync(dto);
            return Ok(result);
        }

        [HttpPost("get-warehouse-byid")]
        public async Task<IActionResult> GetWarehouseById([FromBody] long id)
        {
            var result = await _workFlowService.GetWarehouseByIdAsync(id);
            return Ok(result);
        }
        [HttpGet("get-warehouse-byrequestno")]
        public async Task<IActionResult> GetWarehouseByRequestNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetWarehouseByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("complete-delivery")]
        public async Task<IActionResult> CompleteDelivery([FromBody] YkbCompleteDeliveryDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Geçersiz veri gönderildi." });
            var result = await _workFlowService.CompleteDeliveryAsync(dto);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            return Ok(result);
        }

        [HttpGet("get-workflow-list")]
        public async Task<IActionResult> GetWorkFlowList([FromQuery] QueryParams p)
        {
            var result = await _workFlowService.GetWorkFlowsAsync(p);
            return Ok(result);
        }

        [HttpPost("delete-workflow/{id:long}")]
        public virtual async Task<IActionResult> DeleteWorkFlow([FromRoute] long id)
        {
            var result = await _workFlowService.DeleteWorkFlowAsync(id);
            return Ok(result);
        }

        [HttpPost("cancel-workflow/{id:long}")]
        public async Task<IActionResult> CancelWorkFlow([FromRoute] long id)
        {
            var result = await _workFlowService.CancelWorkFlowAsync(id);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-byid/{id:long}")]
        public async Task<IActionResult> GetServicesRequesById([FromRoute] long id)
        {
            var result = await _workFlowService.GetServiceRequestByIdAsync(id);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-list")]
        public async Task<IActionResult> GetServicesRequestList([FromQuery] QueryParams p)
        {
            var result = await _workFlowService.GetRequestsAsync(p);
            return Ok(result);
        }


        [HttpGet("get-servicesrequest-byrequestno")]
        public async Task<IActionResult> GetServicesRequestByNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetServiceRequestByRequestNoAsync(requestNo);
            return Ok(result);
        }
        [HttpGet("get-customerform-byrequestno")]
        public async Task<IActionResult> GetCustomerFormByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetCustomerFormByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("update-services-request/{id:long}")]
        public async Task<IActionResult> UpdateServicesRequest([FromRoute] long id, [FromBody] YkbServicesRequestUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateServiceRequestAsync(dto);
            return ToActionResult(resp);
        }

        [HttpGet("get-technicalservice-by-requestno")]
        public async Task<IActionResult> GetTechnicalServiceByRequestNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetTechnicalServiceByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("send-technical-service")]
        public async Task<IActionResult> SendTechnicalServiceAsync([FromBody] YkbSendTechnicalServiceDto dto)
        {
            var result = await _workFlowService.SendTechnicalServiceAsync(dto);
            return Ok(result);
        }


        [HttpPost("start-technical-service")]
        public async Task<IActionResult> StartTechnicalServiceAsync([FromBody] YkbStartTechnicalServiceDto dto)
        {
            var result = await _workFlowService.StartService(dto);
            return Ok(result);
        }

        [HttpPost("finish-technical-service")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)] // 200 MB örnek
        [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000, ValueCountLimit = 2048)]
        public async Task<IActionResult> FinishTechnicalServiceAsync([FromForm] YkbFinishTechnicalServiceDto dto)
        {
            var result = await _workFlowService.FinishService(dto);
            return Ok(result);
        }

        [HttpPost("approve-pricing")]
        public async Task<IActionResult> ApprovePricingAsync([FromBody] YkbPricingUpdateDto dto)
        {
            var result = await _workFlowService.ApprovePricing(dto);
            return Ok(result);
        }


        [HttpGet("get-pricing-by-requestno")]
        public async Task<IActionResult> GetPricingByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetPricingByRequestNoAsync(requestNo);
            return Ok(result);
        }


        [HttpPost("final-approve")]
        public async Task<IActionResult> FinalApprove([FromBody] YkbFinalApprovalUpdateDto dto)
        {
            var result = await _workFlowService.FinalApprovalAsync(dto);
            return Ok(result);
        }


        [HttpPost("customer-agreement")]
        public async Task<IActionResult> CustomerAgreementAsync([FromBody] YkbCustomerAgreementDto dto)
        {
            var result = await _workFlowService.CustomerAgreementAsync(dto);
            return Ok(result);
        }




        [HttpGet("get-finalapproval-by-requestno")]
        public async Task<IActionResult> GetFinalApprovalByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetFinalApprovalByRequestNoAsync(requestNo);
            return Ok(result);
        }



        [HttpGet("get-customeragreement-by-requestno")]
        public async Task<IActionResult> GetCustomerAgreementByRequestNoAsync([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetCustomerAgreementByRequestNoAsync(requestNo);
            return Ok(result);
        }

        [HttpGet("get-finalapproval-by-id")]
        public async Task<IActionResult> GetFinalApprovalByIdAsync([FromQuery] long id)
        {
            var result = await _workFlowService.GetFinalApprovalByIdAsync(id);
            return Ok(result);
        }

        [HttpPost("location-override")]
        public async Task<IActionResult> RequestLocationOverrideAsync([FromBody] YkbOverrideLocationCheckDto dto)
        {
            var result = await _workFlowService.RequestLocationOverrideAsync(dto);
            return Ok(result);
        }


        [HttpPost("send-back-for-review")]
        public async Task<IActionResult> SendBackForReviewAsync([FromQuery] string requestNo, [FromQuery] string reviewNotes)
        {
            var result = await _workFlowService.SendBackForReviewAsync(requestNo, reviewNotes);
            return Ok(result);
        }


        [HttpGet("activity-records/{requestNo}")]
        public async Task<IActionResult> GetLatestActivityRecords([FromRoute] string requestNo)
        {
            var result = await _activationRecordService.GetLatestYkbActivityRecordByRequestNoAsync(requestNo);
            return ToActionResult(result);
        }


        // ---------- WorkFlowStep CRUD ----------
        // GET: /api/workflows/steps
        [HttpGet("get-workflow-steps")]
        public async Task<IActionResult> GetSteps([FromQuery] QueryParams q)
        {
            var resp = await _workFlowService.GetStepsAsync(q);
            return ToActionResult(resp);
        }

        [HttpGet("get-workflow-steps/{id:long}")]
        public async Task<IActionResult> GetStepsById([FromRoute] long id)
        {
            var resp = await _workFlowService.GetStepByIdAsync(id);
            return ToActionResult(resp);
        }

        [HttpPost("create-steps")]
        public async Task<IActionResult> CreateSteps([FromBody] YkbWorkFlowStepCreateDto dto)
        {
            var resp = await _workFlowService.CreateStepAsync(dto);

            if (resp.IsSuccess && resp.Data is not null)
                return CreatedAtAction(nameof(GetStepsById), new { id = resp.Data.Id }, resp);

            return ToActionResult(resp);
        }

        [HttpPost("update-steps/{id:long}")]
        public async Task<IActionResult> UpdateSteps([FromRoute] long id, [FromBody] YkbWorkFlowStepUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateStepAsync(dto);
            return ToActionResult(resp);
        }

        [HttpPost("delete-steps/{id:long}")]
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
        [ProducesResponseType(typeof(PagedResult<YkbWorkFlowReportListItemDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get([FromQuery] YkbReportQueryParams q, CancellationToken ct)
        {
            q.Normalize(maxPageSize: 500);

            var result = await _workFlowService.GetReportLinesAsync(q);
            return Ok(result);
        }

        [HttpGet("report-lines/export")]
        public async Task<IActionResult> ExportReportLines([FromQuery] YkbReportQueryParams q)
        {
            var (content, fileName, contentType) = await _workFlowService.ExportReportLinesAsync(q);
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
    }
}
