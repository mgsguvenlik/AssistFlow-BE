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
        Task<ResponseModel<CurrentUserDto>> MeAsync(); // opsiyonel
        Task<ResponseModel<AuthResponseDto>> LoginAsync(LoginRequestDto loginRequest, CancellationToken ct = default);
    }
}
