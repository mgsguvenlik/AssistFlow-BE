using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.User;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : CrudControllerBase<UserCreateDto, UserUpdateDto, UserGetDto, long>
    {
        public UsersController(
       ICrudService<UserCreateDto, UserUpdateDto, UserGetDto, long> service,
       ILogger<UsersController> logger)
       : base(service, logger) { }
    }
}
