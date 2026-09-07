using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Authorization;
using Model.Dtos.CustomerType;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("CustomerTypeList", "CustomerList", "ServiceRequestCreate", "YkbServiceRequestCreate", "EkbServiceRequestCreate", "QnbServiceRequestCreate")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomerTypesController
        : CrudControllerBase<CustomerTypeCreateDto, CustomerTypeUpdateDto, CustomerTypeGetDto, long>
    {
        public CustomerTypesController(
            ICrudService<CustomerTypeCreateDto, CustomerTypeUpdateDto, CustomerTypeGetDto, long> service,
            ILogger<CustomerTypesController> logger)
            : base(service, logger) { }
    }
}
