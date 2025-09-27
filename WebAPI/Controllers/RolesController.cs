using Business.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Role;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController
        : CrudControllerBase<RoleCreateDto, RoleUpdateDto, RoleGetDto, long>
    {
        public RolesController(
            ICrudService<RoleCreateDto, RoleUpdateDto, RoleGetDto, long> service,
            ILogger<RolesController> logger)
            : base(service, logger)
        {
        }

    }
}
