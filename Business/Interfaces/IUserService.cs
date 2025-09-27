using Core.Common;
using Model;
using Model.Dtos.User;

namespace Business.Interfaces
{
    public interface IUserService
    {
        Task<ResponseModel<UserGetDto>> AssignRolesAsync(long userId, IEnumerable<long> roleIds);
    }
}
