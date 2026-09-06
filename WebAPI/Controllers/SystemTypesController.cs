using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Authorization;
using Model.Dtos.SystemType;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("SystemTypeList", "ProductList", "PurchaseRequest", "ServiceRequestCreate", "YkbServiceRequestCreate", "EkbServiceRequestCreate", "QnbServiceRequestCreate")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SystemTypesController
        : CrudControllerBase<SystemTypeCreateDto, SystemTypeUpdateDto, SystemTypeGetDto, long>
    {
        public SystemTypesController(
            ICrudService<SystemTypeCreateDto, SystemTypeUpdateDto, SystemTypeGetDto, long> service,
            ILogger<SystemTypesController> logger) : base(service, logger) { }
    }
}
