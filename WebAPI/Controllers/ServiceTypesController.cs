using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Authorization;
using Model.Dtos.ServiceType;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("ServiceTypeList", "ServiceReportsList", "BasicWorkflowReportsList", "YkbBasicWorkflowReportsList", "QnbBasicWorkflowReportsList", "YkbAccountingServiceReportList", AllowWorkflowRead = true)]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ServiceTypesController
        : CrudControllerBase<ServiceTypeCreateDto, ServiceTypeUpdateDto, ServiceTypeGetDto, long>
    {
        public ServiceTypesController(
            ICrudService<ServiceTypeCreateDto, ServiceTypeUpdateDto, ServiceTypeGetDto, long> service,
            ILogger<ServiceTypesController> logger) : base(service, logger) { }
    }
}
