using Core.Common;
using Model.Dtos.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IAuthService
    {
        Task<ResponseModel<LoginResponseDto>> LoginAsync(LoginRequestDto dto);
        Task<ResponseModel> LogoutAsync();
        Task<ResponseModel<CurrentUserDto>> MeAsync(); // opsiyonel
    }
}
