using Business.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Brand;
using Model.Dtos.WorkFlowDtos.WorkFlowTransition;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkFlowTransitionsController : CrudControllerBase<WorkFlowTransitionCreateDto, WorkFlowTransitionUpdateDto, WorkFlowTransitionGetDto, long>
    {
        public WorkFlowTransitionsController(
            ICrudService<WorkFlowTransitionCreateDto, WorkFlowTransitionUpdateDto, WorkFlowTransitionGetDto, long> service,
            ILogger<WorkFlowTransitionsController> logger) : base(service, logger) { }
    }
}
