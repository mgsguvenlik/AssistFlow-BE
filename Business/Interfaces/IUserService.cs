using Core.Common;
using Model;
using Model.Dtos.User;

namespace Business.Interfaces
{
    public interface IUserService
    {
        Task<ResponseModel<UserGetDto>> AssignRolesAsync(long userId, IEnumerable<long> roleIds);

        Task<ResponseModel<UserGetDto>> SignInAsync(string email, string password);
    }
}
