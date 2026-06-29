using Business.Interfaces;
using Business.Interfaces.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkOrderType;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WorkOrderTypesController
        : CrudControllerBase<
            WorkOrderTypeCreateDto,
            WorkOrderTypeUpdateDto,
            WorkOrderTypeGetDto,
            long>
    {
        public WorkOrderTypesController(
            IWorkOrderTypeService service,
            ILogger<WorkOrderTypesController> logger)
            : base(service, logger)
        {
        }
    }
}