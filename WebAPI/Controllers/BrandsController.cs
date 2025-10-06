using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Brand;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class BrandsController : CrudControllerBase<BrandCreateDto, BrandUpdateDto, BrandGetDto, long>
    {
        public BrandsController(
            ICrudService<BrandCreateDto, BrandUpdateDto, BrandGetDto, long> service,
            ILogger<BrandsController> logger) : base(service, logger) { }
    }
}
