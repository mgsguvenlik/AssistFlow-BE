using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos.WorkFlowArchive;

namespace WebAPI.Controllers
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class WorkFlowArchivesController : ControllerBase
    {
        private readonly IWorkFlowService _workFlowService;
        private readonly ILogger<WorkFlowArchivesController> _logger;

        public WorkFlowArchivesController(
            IWorkFlowService workFlowService,
            ILogger<WorkFlowArchivesController> logger)
        {
            _workFlowService = workFlowService;
            _logger = logger;
        }

        /// <summary>
        /// Arşiv kayıtları liste (filtre + pagination).
        /// Örn: GET api/WorkFlowArchives?requestNo=SR-2025&customerName=YAŞAR&page=1&pageSize=20
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetArchives([FromQuery] WorkFlowArchiveFilterDto filter)
        {
            var result = await _workFlowService.GetArchiveListAsync(filter);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            return Ok(result);
        }

        /// <summary>
        /// Id ile arşiv detayı (tüm snapshot).
        /// GET api/WorkFlowArchives/5
        /// </summary>
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetArchiveDetail(long id)
        {
            var result = await _workFlowService.GetArchiveDetailByIdAsync(id);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            return Ok(result);
        }

        /// <summary>
        /// RequestNo ile arşiv detayı (son arşiv kaydı).
        /// GET api/WorkFlowArchives/by-request-no?requestNo=SR-2025-0001
        /// </summary>
        [HttpGet("by-request-no")]
        public async Task<IActionResult> GetArchiveDetailByRequestNo([FromQuery] string requestNo)
        {
            var result = await _workFlowService.GetArchiveDetailByRequestNoAsync(requestNo);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            return Ok(result);
        }
    }
}
