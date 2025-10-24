using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlowStatus;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkFlowsController : ControllerBase
    {
        private readonly IWorkFlowService _workFlowService;
        public WorkFlowsController(IWorkFlowService workFlowService)
        {
            _workFlowService = workFlowService;
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

        [HttpPost("location-override")]
        public async Task<IActionResult> RequestLocationOverrideAsync([FromBody] OverrideLocationCheckDto dto)
        {
            var result = await _workFlowService.RequestLocationOverrideAsync(dto);
            return Ok(result);
        }

        // ---------- WorkFlowStatus CRUD ----------
        // GET: /api/workflows/statuses
        [HttpGet("get-workflow-statuses")]
        public async Task<IActionResult> GetStatuses([FromQuery] QueryParams q)
        {
            var resp = await _workFlowService.GetStatusesAsync(q);
            return ToActionResult(resp);
        }

        // GET: /api/workflows/statuses/{id}
        [HttpGet("get-workflow-statuses/{id:long}")]
        public async Task<IActionResult> GetStatusById([FromRoute] long id)
        {
            var resp = await _workFlowService.GetStatusByIdAsync(id);
            return ToActionResult(resp);
        }

        // POST: /api/workflows/statuses
        [HttpPost("create-statuses")]
        public async Task<IActionResult> CreateStatus([FromBody] WorkFlowStatusCreateDto dto)
        {
            var resp = await _workFlowService.CreateStatusAsync(dto);

            if (resp.IsSuccess && resp.Data is not null)
                return CreatedAtAction(nameof(GetStatusById), new { id = resp.Data.Id }, resp);

            return ToActionResult(resp);
        }

        // PUT: /api/workflows/statuses/{id}
        [HttpPost("update-statuses/{id:long}")]
        public async Task<IActionResult> UpdateStatus([FromRoute] long id, [FromBody] WorkFlowStatusUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateStatusAsync(dto);
            return ToActionResult(resp);
        }

        // DELETE: /api/workflows/statuses/{id}
        [HttpPost("delete-statuses/{id:long}")]
        public async Task<IActionResult> DeleteStatus([FromRoute] long id)
        {
            var resp = await _workFlowService.DeleteStatusAsync(id);
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
