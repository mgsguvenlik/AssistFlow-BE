using Core.Common;
using Model.Dtos.Auth;

namespace Business.Interfaces
{
    public interface IAuthService
    {
        Task<ResponseModel<CurrentUserDto>> MeAsync(); // opsiyonel
        Task<ResponseModel<AuthResponseDto>> LoginAsync(LoginRequestDto loginRequest, CancellationToken ct = default);
    }
}
