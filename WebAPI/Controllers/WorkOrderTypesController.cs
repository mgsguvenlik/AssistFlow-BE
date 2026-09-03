using Business.Interfaces;
using Business.Interfaces.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkOrderType;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("WorkOrderTypeList", "BasicWorkflowReportsList", "YkbBasicWorkflowReportsList", "QnbBasicWorkflowReportsList", "YkbAccountingServiceReportList", AllowWorkflowRead = true)]
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
