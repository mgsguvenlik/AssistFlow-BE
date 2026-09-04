using Business.Interfaces.Qnb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuAuthorize("QnbServiceRequestArchive", MenuPermission.View)]
    [Route("api/[controller]")]
    [ApiController]
    public class QnbWorkFlowArchivesController : ControllerBase
    {
        private readonly IQnbWorkFlowService _workFlowService;
        private readonly ILogger<QnbWorkFlowArchivesController> _logger;

        public QnbWorkFlowArchivesController(
            IQnbWorkFlowService workFlowService,
            ILogger<QnbWorkFlowArchivesController> logger)
        {
            _workFlowService = workFlowService;
            _logger = logger;
        }

        /// <summary>
        /// Arşiv kayıtları liste (filtre + pagination).
        /// Örn: GET api/QnbWorkFlowArchives?requestNo=QNB-2025&customerName=YAŞAR&page=1&pageSize=20
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetArchives([FromQuery] QnbWorkFlowArchiveFilterDto filter)
        {
            var result = await _workFlowService.GetArchiveListAsync(filter);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            return Ok(result);
        }

        /// <summary>
        /// Id ile arşiv detayı (tüm snapshot).
        /// GET api/QnbWorkFlowArchives/5
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
        /// GET api/QnbWorkFlowArchives/by-request-no?requestNo=QNB-2025-0001
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
