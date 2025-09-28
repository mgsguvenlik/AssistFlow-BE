using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.ProgressApprover;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProgressApproversController
        : CrudControllerBase<ProgressApproverCreateDto, ProgressApproverUpdateDto, ProgressApproverGetDto, long>
    {
        public ProgressApproversController(
            ICrudService<ProgressApproverCreateDto, ProgressApproverUpdateDto, ProgressApproverGetDto, long> service,
            ILogger<ProgressApproversController> logger)
            : base(service, logger) { }
    }
}
