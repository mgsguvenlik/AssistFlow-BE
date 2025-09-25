using Business.Abstractions;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Core.Utilities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Model.Dtos.User;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Business.Services
{
    public class UserService : BaseService, IUserService
    {
        //private readonly IUnitOfWork _unitOfWork;
        //private readonly IMapper _mapper;
        //private readonly IMailService _mailService;
        //private readonly IPasswordHasherService _passwordHasherService;
        public UserService(IUnitOfWork uow,  IPasswordHasherService passwordHasherService) : base(uow)
        {
          
        }
        //public ResponseModel<UserGetDto> GetById<TEntity>(object id, bool asNoTracking = false) where TEntity : class
        //{
        //    var entity = _unitOfWork.Repository.GetById<User>(asNoTracking, id);
        //    var userDto = _mapper.Map<UserGetDto>(entity);
        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = userDto,
        //        IsSuccess = entity != null,
        //        StatusCode = entity != null ? Core.Enums.StatusCode.Success : Core.Enums.StatusCode.NotFound,
        //        Message = entity != null ? "User retrieved successfully." : "User not found."
        //    };
        //}
        //public async Task<ResponseModel<UserGetDto>> GetByIdAsync(object id, bool asNoTracking = false, CancellationToken cancellationToken = default)
        //{
        //    var entity = _unitOfWork.Repository.GetMultiple<User>(
        //        false,
        //        x => x.Id == (Guid)id,
        //        q => q.Include(u => u.Roles)
        //    ).FirstOrDefault();
        //    var userDto = _mapper.Map<UserGetDto>(entity);
        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = userDto,
        //        IsSuccess = entity != null,
        //        StatusCode = entity != null ? Core.Enums.StatusCode.Success : Core.Enums.StatusCode.NotFound,
        //        Message = entity != null ? "User retrieved successfully." : "User not found."
        //    };
        //}
        //public ResponseModel<List<UserGetDto>> GetAll(bool asNoTracking = false)
        //{
        //    var list = _unitOfWork.Repository.GetMultiple<User>(asNoTracking, q => q.Include(u => u.Roles));
        //    var userDtoList = _mapper.Map<List<UserGetDto>>(list);
        //    return new ResponseModel<List<UserGetDto>>
        //    {
        //        Data = userDtoList,
        //        IsSuccess = true,
        //        StatusCode = Core.Enums.StatusCode.Success,
        //        Message = list != null && list.Count > 0 ? "Users retrieved successfully." : "No users found."
        //    };
        //}
        //public async Task<ResponseModel<List<UserGetDto>>> GetAllAsync(bool asNoTracking = false, CancellationToken cancellationToken = default)
        //{
        //    var list = await _unitOfWork.Repository.GetMultipleAsync<User>(asNoTracking, q => q.Include(u => u.Roles), cancellationToken);
        //    var userDtoList = _mapper.Map<List<UserGetDto>>(list);
        //    return new ResponseModel<List<UserGetDto>>
        //    {
        //        Data = userDtoList,
        //        IsSuccess = list != null && list.Count > 0,
        //        StatusCode = (list != null && list.Count > 0) ? Core.Enums.StatusCode.Success : Core.Enums.StatusCode.NotFound,
        //        Message = (list != null && list.Count > 0) ? "Users retrieved successfully." : "No users found."
        //    };
        //}
        //public async Task<ResponseModel<UserGetDto>> AddAsync(UserCreateDto dto)
        //{
        //    var user = _mapper.Map<User>(dto);
        //    // Attach existing roles if any are provided in the DTO
        //    if (dto.Roles != null && dto.Roles.Any())
        //    {
        //        // Fetch all roles matching the provided IDs in a single query
        //        var roles = await _unitOfWork.Repository.GetMultipleAsync<Role>(
        //            asNoTracking: false,
        //            whereExpression: r => dto.Roles.Contains(r.Id)
        //        );

        //        user.Roles = roles ?? new List<Role>();
        //    }
        //    else
        //    {
        //        user.Roles = new List<Role>();
        //    }

        //    user.Password = _passwordHasherService.HashPassword(Guid.NewGuid().ToString("N").Substring(0, 8));
        //    //user.Password = _passwordHasherService.HashPassword("123456");
        //    user.UserName = dto.FirstName.ToLower() + "." + dto.LastName.ToLower();


        //    var mailBody = $@"
        //        Merhaba {user.FirstName},
        //        <br/><br/>
        //        Hesabınız başarıyla oluşturuldu. Şifreniz: {user.Password}<br/>
        //        Lütfen şifrenizi değiştirin ve hesabınızı güvenli tutun.<br/><br/>
        //        Saygılarımızla,<br/>";

        //    var mailResult = await _mailService.SendMailAsync(mailBody);

        //    _unitOfWork.Repository.Add(user);
        //    var result = await SaveAsync(user);
        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = _mapper.Map<UserGetDto>(result.Data),
        //        IsSuccess = result.IsSuccess,
        //        StatusCode = result.StatusCode,
        //        Message = result.Message
        //    };
        //}
        //public async Task<ResponseModel<UserGetDto>> UpdateAsync(UserUpdateDto userUpdate)
        //{
        //    // Get the existing user including roles
        //    var user = _unitOfWork.Repository.GetSingle<User>(
        //        asNoTracking: false,
        //        whereExpression: u => u.Id == userUpdate.Id,
        //        q => q.Include(u => u.Roles)
        //    );

        //    if (user == null)
        //    {
        //        return new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.NotFound,
        //            Message = "User not found."
        //        };
        //    }

        //    // Update scalar properties
        //    _mapper.Map(userUpdate, user);

        //    // Update roles
        //    if (userUpdate.Roles != null)
        //    {
        //        var roleIds = userUpdate.Roles.Select(r => r.Id).ToList();
        //        var roles = await _unitOfWork.Repository.GetMultipleAsync<Role>(
        //        asNoTracking: false,
        //        whereExpression: r => roleIds.Contains(r.Id));

        //        user.Roles = roles?.ToList() ?? new List<Role>();
        //    }
        //    else
        //    {
        //        user.Roles = new List<Role>();
        //    }

        //    _unitOfWork.Repository.Update(user);
        //    var result = await SaveAsync(user);

        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = _mapper.Map<UserGetDto>(result.Data),
        //        IsSuccess = result.IsSuccess,
        //        StatusCode = result.StatusCode,
        //        Message = result.Message
        //    };
        //}
        //public async Task<ResponseModel> DeleteAsync(object id)
        //{
        //    _unitOfWork.Repository.HardDelete<User>(id);
        //    var result = await SaveAsync();
        //    return new ResponseModel
        //    {
        //        IsSuccess = result.IsSuccess,
        //        StatusCode = result.StatusCode,
        //        Message = result.Message
        //    };
        //}
        //public async Task<ResponseModel<UserGetDto>> SignInAsync(string email, string password)
        //{
        //    // Find user by email and include roles
        //    var user = _unitOfWork.Repository.GetMultiple<User>(
        //        asNoTracking: false,
        //        whereExpression: u => u.Email == email,
        //        q => q.Include(u => u.Roles)
        //    ).FirstOrDefault();
        //    if (user == null || !_passwordHasherService.VerifyPassword(user.Password, password))
        //    {
        //        return new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.NotFound,
        //            Message = "Invalid email or password."
        //        };
        //    }

        //    user.LastLoginAt = DateTime.UtcNow;
        //    _unitOfWork.Repository.Update(user);
        //    await _unitOfWork.Repository.CompleteAsync();


        //    var userDto = _mapper.Map<UserGetDto>(user);
        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = userDto,
        //        IsSuccess = true,
        //        StatusCode = Core.Enums.StatusCode.Success,
        //        Message = "Sign in successful."
        //    };
        //}

        //public async Task<ResponseModel<UserGetDto>> BasicAuthAsync(string userName, string password)
        //{
        //    // Find user by email and include roles
        //    var user = _unitOfWork.Repository.GetMultiple<User>(
        //        asNoTracking: false,
        //        whereExpression: u => u.UserName == userName,
        //        q => q.Include(u => u.Roles)
        //    ).FirstOrDefault();
        //    if (user == null || user.Password != password)
        //    {
        //        return new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.NotFound,
        //            Message = "Invalid user or password."
        //        };
        //    }
        //    var userDto = _mapper.Map<UserGetDto>(user);
        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = userDto,
        //        IsSuccess = true,
        //        StatusCode = Core.Enums.StatusCode.Success,
        //        Message = "Sign in successful."
        //    };
        //}

        //public async Task<ResponseModel<UserGetDto>> ResetPasswordRequestAsync(string email, CancellationToken cancellationToken = default)
        //{
        //    var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
        //    var user = _unitOfWork.Repository.GetMultiple<User>(
        //        asNoTracking: false,
        //        whereExpression: u => u.Email == email
        //    ).FirstOrDefault();

        //    if (user == null)
        //    {
        //        return new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.NotFound,
        //            Message = "User not found."
        //        };
        //    }

        //    var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Email ?? string.Empty) };

        //    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.Value.Key));
        //    var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        //    var token = new JwtSecurityToken(
        //        issuer: appSettings.Value.Issuer,
        //        audience: appSettings.Value.Audience,
        //        claims: claims,
        //        expires: DateTime.UtcNow.AddMinutes(15),
        //        signingCredentials: creds
        //    );

        //    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        //    var mailBody = $@"
        //        Merhaba {user.FirstName},
        //        <br/><br/>
        //        Şifrenizi sıfırlamak için lütfen aşağıdaki bağlantıya tıklayın:<br/>
        //        <a href='https://admin.medalarm.com.tr/#/change-password/{tokenString}'>Şifre Sıfırlama Bağlantısı</a><br/><br/>
        //        Eğer bu isteği siz yapmadıysanız, lütfen bu e-postayı dikkate almayın.<br/><br/>
        //        Saygılarımızla,<br/>
        //    ";

        //    var mailResult = await _mailService.SendMailAsync(mailBody);

        //    if (!mailResult.IsSuccess)
        //    {
        //        return new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.Error,
        //            Message = "Failed to send reset password email."
        //        };
        //    }

        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = _mapper.Map<UserGetDto>(user),
        //        IsSuccess = true,
        //        StatusCode = Core.Enums.StatusCode.Success,
        //        Message = "Reset password request processed successfully. Please check your email."
        //    };
        //}

        //public Task<ResponseModel<UserGetDto>> ChangePasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
        //{


        //    var handler = new JwtSecurityTokenHandler();
        //    var jwtToken = handler.ReadJwtToken(token);


        //    var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        //    var userEmail = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        //    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
        //    {
        //        return Task.FromResult(new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.Error,
        //            Message = "Invalid token."
        //        });
        //    }

        //    var user = _unitOfWork.Repository.GetMultiple<User>(
        //    asNoTracking: true,
        //    whereExpression: u => u.Id == Guid.Parse(userId) && u.Email == userEmail
        //    ).FirstOrDefault();


        //    if (user == null)
        //    {
        //        return Task.FromResult(new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.NotFound,
        //            Message = "Invalid reset token."
        //        });
        //    }

        //    if (jwtToken.ValidTo < DateTime.UtcNow)
        //    {
        //        return Task.FromResult(new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.Error,
        //            Message = "Token expired."
        //        });
        //    }

        //    if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        //    {
        //        return Task.FromResult(new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.Error,
        //            Message = "New password must be at least 6 characters long."
        //        });
        //    }

        //    if (user.Password == newPassword)
        //    {
        //        return Task.FromResult(new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.Error,
        //            Message = "New password cannot be the same as the old password."
        //        });
        //    }

        //    user.Password = newPassword;
        //    _unitOfWork.Repository.Update(user);
        //    var result = SaveAsync(user).Result;

        //    if (!result.IsSuccess)
        //    {
        //        return Task.FromResult(new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.Error,
        //            Message = "Failed to change password."
        //        });
        //    }


        //    return Task.FromResult(new ResponseModel<UserGetDto>
        //    {
        //        Data = _mapper.Map<UserGetDto>(user),
        //        IsSuccess = true,
        //        StatusCode = Core.Enums.StatusCode.Success,
        //        Message = "Password changed successfully."
        //    });
        //}

        //public async Task<ResponseModel<UserGetDto>> SelfUpdateAsync(UserSelfUpdateDto entity)
        //{
        //    // Get the existing user including roles
        //    var user = _unitOfWork.Repository.GetSingle<User>(asNoTracking: false, whereExpression: u => u.Id == entity.Id);

        //    if (user == null)
        //    {
        //        return new ResponseModel<UserGetDto>
        //        {
        //            Data = null,
        //            IsSuccess = false,
        //            StatusCode = Core.Enums.StatusCode.NotFound,
        //            Message = "User not found."
        //        };
        //    }

        //    _mapper.Map(entity, user);


        //    _unitOfWork.Repository.Update(user);
        //    var result = await SaveAsync(user);

        //    return new ResponseModel<UserGetDto>
        //    {
        //        Data = _mapper.Map<UserGetDto>(result.Data),
        //        IsSuccess = result.IsSuccess,
        //        StatusCode = result.StatusCode,
        //        Message = result.Message
        //    };
        //}
    }
}
