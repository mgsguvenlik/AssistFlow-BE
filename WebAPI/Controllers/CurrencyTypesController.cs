// WebAPI/Controllers/CurrencyTypesController.cs
using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Model.Dtos.CurrencyType;

namespace WebAPI.Controllers
{
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
