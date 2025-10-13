using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
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
        public async Task<IActionResult> CreateRequest([FromBody] Model.Dtos.WorkFlowDtos.ServicesRequest.ServicesRequestCreateDto dto)
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


        [HttpGet("get-workflow-list")]
        public async Task<IActionResult> GetWorkFlowList([FromQuery] QueryParams p)
        {
            var result = await _workFlowService.GetWorkFlowsAsync(p);
            return Ok(result);
        }

        [HttpGet("get-servicesrequest-getbyid/{id:long}")]
        public async Task<IActionResult> GetServicesRequesById([FromRoute] long id)
        {
            var result = await _workFlowService.GetRequestByIdAsync(id);
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
            var result = await _workFlowService.GetRequestByNoAsync(requestNo);
            return Ok(result);
        }

        [HttpPost("update-services-request/{id:long}")]
        public async Task<IActionResult> UpdateServicesRequest([FromRoute] long id, [FromBody] ServicesRequestUpdateDto dto)
        {
            if (dto.Id != id)
                return BadRequest(new ResponseModel(false, "Route id ile body id eşleşmiyor.", Core.Enums.StatusCode.BadRequest));

            var resp = await _workFlowService.UpdateRequestAsync(dto);
            return ToActionResult(resp);
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
