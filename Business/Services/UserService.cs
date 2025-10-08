using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Settings.Concrete;
using Core.Utilities.Constants;
using Core.Utilities.IoC;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Model.Concrete;
using Model.Dtos.User;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;

public class UserService
    : CrudServiceBase<User, long, UserCreateDto, UserUpdateDto, UserGetDto>, IUserService
{

    private readonly IMailService _mailService;

    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, Microsoft.AspNetCore.Identity.IPasswordHasher<User> passwordHasher, IMailService mailService)
        : base(uow, mapper, config)
    {
        _passwordHasher = passwordHasher;
        _mailService = mailService;
    }

    protected override long ReadKey(User entity) => entity.Id;

    protected override Expression<Func<User, bool>> KeyPredicate(long id)
        => u => u.Id == id;

    public override async Task<ResponseModel<UserGetDto>> CreateAsync(UserCreateDto dto)
    {
        try
        {
            // 1) DTO -> Entity
            var entity = _mapper.Map<User>(dto);

            // 2) Şifre zorunlu + hashle
            if (string.IsNullOrWhiteSpace(dto.Password))
                return ResponseModel<UserGetDto>.Fail(Messages.PasswordRequired, StatusCode.BadRequest);

            entity.PasswordHash = _passwordHasher.HashPassword(entity, dto.Password);

            // (Opsiyonel) Eğer base'inde SetCreateAuditIfExists varsa çağır:
            // SetCreateAuditIfExists(entity);

            // 3) Kullanıcıyı kaydet (Id üretilecek)
            await _unitOfWork.Repository.AddAsync(entity);
            await _unitOfWork.Repository.CompleteAsync();

            // 4) Rol atamaları (varsa) -> önce doğrula sonra ekle
            if (dto.RoleIds is not null && dto.RoleIds.Any())
            {
                var desired = dto.RoleIds.Distinct().ToList();

                // Geçerli rol Id'lerini getir
                var roles = await _unitOfWork.Repository.GetMultipleAsync<Role>(
                    asNoTracking: true,
                    r => desired.Contains(r.Id));

                var existingIds = roles.Select(r => r.Id).ToHashSet();
                var missing = desired.Where(id => !existingIds.Contains(id)).ToList();
                if (missing.Any())
                    return ResponseModel<UserGetDto>.Fail(
                        $"{Messages.InvalidRoleIds}: {string.Join(", ", missing)}",
                        StatusCode.BadRequest);

                // UserRole kayıtlarını ekle
                var userRoles = existingIds.Select(rid => new UserRole
                {
                    UserId = entity.Id,
                    RoleId = rid
                }).ToList();

                if (userRoles.Count > 0)
                {
                    await _unitOfWork.Repository.AddRangeAsync(userRoles);
                    await _unitOfWork.Repository.CompleteAsync();
                }
            }

            // 5) DTO'yu include'larla projekte et ve döndür
            var query = _unitOfWork.Repository.GetQueryable<User>();
            var inc = IncludeExpression();
            if (inc is not null) query = inc(query);

            var created = await query.AsNoTracking()
                                     .Where(u => u.Id == entity.Id)
                                     .ProjectToType<UserGetDto>(_config)
                                     .FirstAsync();

            return ResponseModel<UserGetDto>.Success(created, Messages.Created, StatusCode.Created);
        }
        catch (DbUpdateException ex)
        {
            return ResponseModel<UserGetDto>.Fail($"{Messages.DatabaseError}: {ex.Message}", StatusCode.Conflict);
        }
        catch (Exception ex)
        {
            return ResponseModel<UserGetDto>.Fail($"{Messages.UnexpectedError}: {ex.Message}", StatusCode.Error);
        }
    }

    // Include'lar (roller)
    protected override Func<IQueryable<User>, IIncludableQueryable<User, object>>? IncludeExpression()
        => q => q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role);
    protected override async Task<User?> ResolveEntityForUpdateAsync(UserUpdateDto dto)
    {
        if (dto.Id <= 0) return null;

        // 1) PK meta-cast ile güvenli getirme (include + theninclude)
        var user = await _unitOfWork.Repository.GetByIdAsync<User>(
            asNoTracking: false,
            id: dto.Id,
            includeExpression: q => q
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
        );

        if (user != null) return user;

        // 2) DIAGNOSTIC: Eğer null geldiyse, global filtrelerin eliyor olma ihtimalini test et
        //    Repo’yu bypass ederek IgnoreQueryFilters ile deneyin.
        user = await _unitOfWork.Repository
            .GetQueryable<User>()            // DbSet<User>()
            .IgnoreQueryFilters()            // <- soft delete / tenant filtresi varsa geçici olarak atla
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == (int)dto.Id); // PK int ise açık cast yapın

        return user;
    }
    protected override void MapUpdate(UserUpdateDto dto, User entity)
    {
        // mapster partial update (IgnoreNullValues(true) konfig’de)
        base.MapUpdate(dto, entity);

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            entity.PasswordHash = _passwordHasher.HashPassword(entity, dto.NewPassword);

        if (dto.RoleIds is not null)
        {
            var current = entity.UserRoles.Select(ur => ur.RoleId).ToHashSet();
            var desired = dto.RoleIds.ToHashSet();

            var toAdd = desired.Except(current);
            var toRemove = current.Except(desired);

            if (toRemove.Any())
                entity.UserRoles = entity.UserRoles
                    .Where(ur => !toRemove.Contains(ur.RoleId))
                    .ToList();

            foreach (var rid in toAdd)
                entity.UserRoles.Add(new UserRole { UserId = entity.Id, RoleId = rid });
        }
    }

    // ---------- Çoklu Rol Atama (Id listesi) ----------
    public async Task<ResponseModel<UserGetDto>> AssignRolesAsync(long userId, IEnumerable<long> roleIds)
    {
        try
        {
            var desired = (roleIds ?? Enumerable.Empty<long>()).Distinct().ToHashSet();

            var user = await _unitOfWork.Repository.GetSingleAsync<User>(
                asNoTracking: false,
                u => u.Id == userId,
                q => q.Include(u => u.UserRoles));

            if (user is null)
                return ResponseModel<UserGetDto>.Fail(Messages.UserNotFound, StatusCode.NotFound);

            // Var olan role’leri doğrula (geçersiz id varsa bildir)
            var existingRoles = await _unitOfWork.Repository.GetMultipleAsync<Role>(
                asNoTracking: true,
                r => desired.Contains(r.Id));

            var existingIds = existingRoles.Select(r => r.Id).ToHashSet();
            var missing = desired.Except(existingIds).ToList();
            if (missing.Any())
                return ResponseModel<UserGetDto>.Fail($"{Messages.UnknownRoleIds}: {string.Join(", ", missing)}", StatusCode.BadRequest);

            // Senkronize et
            var current = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
            var toAdd = existingIds.Except(current);
            var toRemove = current.Except(existingIds);

            if (toRemove.Any())
                user.UserRoles = user.UserRoles.Where(ur => !toRemove.Contains(ur.RoleId)).ToList();

            foreach (var rid in toAdd)
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rid });

            await _unitOfWork.Repository.CompleteAsync();

            // Güncel DTO’yu include ile getir
            var query = _unitOfWork.Repository.GetQueryable<User>();
            query = IncludeExpression()!(query);

            var dto = await query.AsNoTracking()
                                 .Where(u => u.Id == userId)
                                 .ProjectToType<UserGetDto>(_config)
                                 .FirstAsync();

            return ResponseModel<UserGetDto>.Success(dto, Messages.RolesUpdated);
        }
        catch (Exception ex)
        {
            return ResponseModel<UserGetDto>.Fail($"{Messages.UnexpectedError} {ex.Message}", StatusCode.Error);
        }
    }



    //  Login (email + şifre ile)
    public async Task<ResponseModel<UserGetDto>> SignInAsync(string email, string password)
    {
        // Find user by email and include roles
        var user = _unitOfWork.Repository.GetMultiple<User>(
            asNoTracking: false,
            whereExpression: u => u.TechnicianEmail == email,
            q => q.Include(u => u.UserRoles).ThenInclude(x => x.Role)
        ).FirstOrDefault();

        if (user == null)
        {
            return new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.NotFound,
                Message = Messages.InvalidEmailOrPassword
            };
        }


        var vr = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (vr == PasswordVerificationResult.Failed)
        {
            return new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.NotFound,
                Message = Messages.InvalidEmailOrPassword
            };
        }

        var userDto = _mapper.Map<UserGetDto>(user);
        return new ResponseModel<UserGetDto>
        {
            Data = userDto,
            IsSuccess = true,
            StatusCode = Core.Enums.StatusCode.Ok,
            Message = Messages.SignInSuccessful
        };
    }


    // Şifre Sıfırlama İsteği (email gönderimi)
    public async Task<ResponseModel<UserGetDto>> ResetPasswordRequestAsync(string email, CancellationToken cancellationToken = default)
    {
        var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
        var user = _unitOfWork.Repository.GetMultiple<User>(
            asNoTracking: false,
            whereExpression: u => u.TechnicianEmail == email
        ).FirstOrDefault();

        if (user == null)
        {
            return new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.NotFound,
                Message = Messages.UserNotFound
            };
        }

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.TechnicianEmail ?? string.Empty) };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.Value.Key));
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: appSettings.Value.Issuer,
            audience: appSettings.Value.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var mailBody = $@"
                Merhaba {user.TechnicianName},
                <br/><br/>
                Şifrenizi sıfırlamak için lütfen aşağıdaki bağlantıya tıklayın:<br/>
                <a href='{appSettings.Value.AppUrl}/change-password/{tokenString}'>Şifre Sıfırlama Bağlantısı</a><br/><br/>
                Eğer bu isteği siz yapmadıysanız, lütfen bu e-postayı dikkate almayın.<br/><br/>
                Saygılarımızla,<br/>
            ";

        var mailResult = await _mailService.SendResetPassMailAsync(mailBody, user.TechnicianEmail);

        if (!mailResult.IsSuccess)
        {
            return new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.Error,
                Message = Messages.FailedToSendResetPasswordEmail + tokenString //MZK geçici test aşamasında çözüm için 
            };
        }

        return new ResponseModel<UserGetDto>
        {
            Data = _mapper.Map<UserGetDto>(user),
            IsSuccess = true,
            StatusCode = Core.Enums.StatusCode.Ok,
            Message = Messages.ResetPasswordRequestSuccess
        };
    }

    public Task<ResponseModel<UserGetDto>> ChangePasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);


        var userIdString = (jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);
        var userEmail = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userIdString) || string.IsNullOrEmpty(userEmail))
        {
            return Task.FromResult(new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.Error,
                Message = Messages.InvalidToken
            });
        }
        long userId = long.Parse(userIdString);
        var user = _unitOfWork.Repository.GetMultiple<User>(
        asNoTracking: true,
        whereExpression: u => u.Id == userId && u.TechnicianEmail == userEmail
        ).FirstOrDefault();


        if (user == null)
        {
            return Task.FromResult(new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.NotFound,
                Message = Messages.InvalidResetToken
            });
        }

        if (jwtToken.ValidTo < DateTime.UtcNow)
        {
            return Task.FromResult(new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.Error,
                Message = Messages.TokenExpired
            });
        }

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        {
            return Task.FromResult(new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.Error,
                Message = Messages.NewPasswordTooShort
            });
        }

        if (user.PasswordHash == _passwordHasher.HashPassword(user, newPassword))
        {
            return Task.FromResult(new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.Error,
                Message = Messages.NewPasswordCannotBeSameAsOld
            });
        }


        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword); ;
        _unitOfWork.Repository.Update(user);
        var result = SaveAsync<User>(user).Result;

        if (!result.IsSuccess)
        {
            return Task.FromResult(new ResponseModel<UserGetDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = Core.Enums.StatusCode.Error,
                Message = Messages.FailedToChangePassword
            });
        }


        return Task.FromResult(new ResponseModel<UserGetDto>
        {
            Data = _mapper.Map<UserGetDto>(user),
            IsSuccess = true,
            StatusCode = Core.Enums.StatusCode.Ok,
            Message = Messages.PasswordChangedSuccessfully
        });
    }


}
