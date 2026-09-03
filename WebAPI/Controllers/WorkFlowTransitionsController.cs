using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos.WorkFlowTransition;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("FlowStatusList")]
    [Route("api/[controller]")]
    [ApiController]
    public class WorkFlowTransitionsController : CrudControllerBase<WorkFlowTransitionCreateDto, WorkFlowTransitionUpdateDto, WorkFlowTransitionGetDto, long>
    {
        public WorkFlowTransitionsController(
            ICrudService<WorkFlowTransitionCreateDto, WorkFlowTransitionUpdateDto, WorkFlowTransitionGetDto, long> service,
            ILogger<WorkFlowTransitionsController> logger) : base(service, logger) { }
    }
}
