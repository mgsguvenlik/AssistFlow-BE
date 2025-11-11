using Business.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Menu;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MenusController : CrudControllerBase<MenuCreateDto, MenuUpdateDto, MenuGetDto, long>
    {
        public MenusController(
            ICrudService<MenuCreateDto, MenuUpdateDto, MenuGetDto, long> service,
            ILogger<MenusController> logger) : base(service, logger) { }
    }
}
