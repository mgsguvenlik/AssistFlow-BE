using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Tenant;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TenantsController
        : CrudControllerBase<TenantCreateDto, TenantUpdateDto, TenantGetDto, long>
    {
        public TenantsController(
            ICrudService<TenantCreateDto, TenantUpdateDto, TenantGetDto, long> service,
            ILogger<TenantsController> logger)
            : base(service, logger)
        {
        }

        // 🚩 CREATE: multipart/form-data + FromForm
        [HttpPost]
        [Consumes("multipart/form-data")]
        public override async Task<IActionResult> Create([FromForm] TenantCreateDto dto)
        {
            return await base.Create(dto);
        }

        // 🚩 UPDATE: multipart/form-data + FromForm
        [HttpPost("update")]
        [Consumes("multipart/form-data")]
        public override async Task<IActionResult> Update([FromForm] TenantUpdateDto dto)
        {
            return await base.Update(dto);
        }
    }
}
