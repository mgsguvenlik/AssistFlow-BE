using Business.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Brand;
using Model.Dtos.Model;

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
