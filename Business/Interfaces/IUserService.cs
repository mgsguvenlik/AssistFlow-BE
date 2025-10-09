using Core.Common;
using Model.Dtos.User;

namespace Business.Interfaces
{
    public interface IUserService : ICrudService<UserCreateDto, UserUpdateDto, UserGetDto, long>
    {
        Task<ResponseModel<UserGetDto>> AssignRolesAsync(long userId, IEnumerable<long> roleIds);
        Task<ResponseModel<UserGetDto>> SignInAsync(string email, string password);
        Task<ResponseModel<UserGetDto>> ChangePasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
        Task<ResponseModel<UserGetDto>> ResetPasswordRequestAsync(string email, CancellationToken cancellationToken = default);

        Task<ResponseModel<UserGetDto>> ChangePasswordWithOldAsync(
                long userId,
                string oldPassword,
                string newPassword,
                string newPasswordConfirm,
                CancellationToken cancellationToken = default
        );
    }
}
