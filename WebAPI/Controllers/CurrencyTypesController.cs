using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Authorization;
using Model.Dtos.CurrencyType;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("CurrencyTypeList", "ProductList", "PurchaseRequest", "ServiceRequestCreate", "YkbServiceRequestCreate", "QnbServiceRequestCreate")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CurrencyTypesController
        : CrudControllerBase<CurrencyTypeCreateDto, CurrencyTypeUpdateDto, CurrencyTypeGetDto, long>
    {
        public CurrencyTypesController(
            ICrudService<CurrencyTypeCreateDto, CurrencyTypeUpdateDto, CurrencyTypeGetDto, long> service,
            ILogger<CurrencyTypesController> logger) : base(service, logger) { }
    }
}
