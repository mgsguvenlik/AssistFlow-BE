using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.ProgressApprover;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProgressApproversController : CrudControllerBase<ProgressApproverCreateDto, ProgressApproverUpdateDto, ProgressApproverGetDto, long>
    {

        private IProgressApproverService _progressApproverService;
        public ProgressApproversController(ICrudService<ProgressApproverCreateDto,
            ProgressApproverUpdateDto, ProgressApproverGetDto, long> service, ILogger<ProgressApproversController> logger, IProgressApproverService progressApproverService) : base(service, logger)
        {
            _progressApproverService = progressApproverService;
        }

        [HttpGet("getby-customerid")]
        public  async Task<IActionResult> GetByCustomerId(long customerId)
        {
            var resp = await _progressApproverService.GetByCustomerIdAsync(customerId, CancellationToken.None);
            return ToActionResult(resp);

        }
    }

}
