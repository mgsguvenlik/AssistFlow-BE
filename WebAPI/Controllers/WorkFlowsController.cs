using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos.Pricing;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using Model.Dtos.WorkFlowDtos.WorkFlowStep;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkFlowsController : ControllerBase
    {
        private readonly IWorkFlowService _workFlowService;
        private readonly IActivationRecordService _activationRecordService;
        public WorkFlowsController(IWorkFlowService workFlowService, IActivationRecordService activationRecordService)
        {
            _workFlowService = workFlowService;
            _activationRecordService = activationRecordService;
        }

        [HttpGet("generate-request-no")]
        public async Task<IActionResult> GetFlowRequestNo(string prfeix = "SR")
        {
            var result = await _workFlowService.GetRequestNoAsync(prfeix);
            return Ok(result);
        }

        [HttpPost("create-services-request")]
        public async Task<IActionResult> CreateRequest([FromBody] ServicesRequestCreateDto dto)
        {
            var result = await _workFlowService.CreateRequestAsync(dto);
            return Ok(result);
        }

        [HttpPost("send-warehouse")]
        public async Task<IActionResult> SendWarehouse([FromBody] SendWarehouseDto dto)
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
        public async Task<IActionResult> CompleteDelivery([FromBody] CompleteDeliveryDto dto)
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
            var result = await _workFlowService.GetServiceRequestByNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("update-services-request/{id:long}")]
        public async Task<IActionResult> UpdateServicesRequest([FromRoute] long id, [FromBody] ServicesRequestUpdateDto dto)
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
        public async Task<IActionResult> SendTechnicalServiceAsync([FromBody] SendTechnicalServiceDto dto)
        {
            var result = await _workFlowService.SendTechnicalServiceAsync(dto);
            return Ok(result);
        }


        [HttpPost("start-technical-service")]
        public async Task<IActionResult> StartTechnicalServiceAsync([FromBody] StartTechnicalServiceDto dto)
        {
            var result = await _workFlowService.StartService(dto);
            return Ok(result);
        }

        [HttpPost("finish-technical-service")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)] // 200 MB örnek
        [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000, ValueCountLimit = 2048)]
        public async Task<IActionResult> FinishTechnicalServiceAsync([FromForm] FinishTechnicalServiceDto dto)
        {
            var result = await _workFlowService.FinishService(dto);
            return Ok(result);
        }



        [HttpPost("approve-pricing")]
        public async Task<IActionResult> ApprovePricingAsync([FromBody] PricingUpdateDto dto)
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



        [HttpPost("location-override")]
        public async Task<IActionResult> RequestLocationOverrideAsync([FromBody] OverrideLocationCheckDto dto)
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
            var result = await _activationRecordService.GetLatestActivityRecordByRequestNoAsync(requestNo);
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



        // GET: /api/workflows/steps/{id}
        [HttpGet("get-workflow-steps/{id:long}")]
        public async Task<IActionResult> GetStepsById([FromRoute] long id)
        {
            var resp = await _workFlowService.GetStepByIdAsync(id);
            return ToActionResult(resp);
        }

        // POST: /api/workflows/steps
        [HttpPost("create-steps")]
        public async Task<IActionResult> CreateSteps([FromBody] WorkFlowStepCreateDto dto)
        {
            var resp = await _workFlowService.CreateStepAsync(dto);

            if (resp.IsSuccess && resp.Data is not null)
                return CreatedAtAction(nameof(GetStepsById), new { id = resp.Data.Id }, resp);

            return ToActionResult(resp);
        }

        // PUT: /api/workflows/steps/{id}
        [HttpPost("update-steps/{id:long}")]
        public async Task<IActionResult> UpdateSteps([FromRoute] long id, [FromBody] WorkFlowStepUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateStepAsync(dto);
            return ToActionResult(resp);
        }

        // DELETE: /api/workflows/steps/{id}
        [HttpPost("delete-steps/{id:long}")]
        public async Task<IActionResult> DeleteSteps([FromRoute] long id)
        {
            var resp = await _workFlowService.DeleteStepAsync(id);
            if (resp.IsSuccess && resp.StatusCode == Core.Enums.StatusCode.Ok)
                return NoContent();

            return ToActionResult(resp);
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
    }
}
