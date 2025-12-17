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
    public UserService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IPasswordHasher<User> passwordHasher, IMailService mailService)
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



            var uniqueErr = await EnsureUniqueTechnicianAsync(
                currentUserId: 0,
                technicianCode: entity.TechnicianCode,
                technicianEmail: entity.TechnicianEmail,
                ct: CancellationToken.None // CreateAsync imzanda ct yoksa None; varsa ct ver
            );

            if (uniqueErr != null)
                return uniqueErr;
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
    public override async Task<ResponseModel<UserGetDto>> UpdateAsync(UserUpdateDto dto)
    {
        try
        {
            var entity = await ResolveEntityForUpdateAsync(dto);
            if (entity is null)
                return ResponseModel<UserGetDto>.Fail(Messages.RecordNotFound, StatusCode.NotFound);

            // dto -> entity map
            MapUpdate(dto, entity);


            // TenantId nullable olabilir: null -> bırak, 0 -> null yap, >0 -> var mı kontrol et
            if (dto.TenantId.HasValue)
            {
                if (dto.TenantId.Value <= 0)
                {
                    entity.TenantId = null; // 0 veya negatif geldiyse FK'yi bozmamak için null'a çevir
                }
                else
                {
                    var tenantExists = await _repo.GetQueryable<Tenant>()
                        .AsNoTracking()
                        .AnyAsync(t => t.Id == dto.TenantId.Value);

                    if (!tenantExists)
                        return ResponseModel<UserGetDto>.Fail("Geçersiz TenantId. Böyle bir tenant yok.", StatusCode.BadRequest);

                    entity.TenantId = dto.TenantId.Value;
                }
            }
            else
            {
                entity.TenantId = null;
            }


            // ✅ UNIQUE kontrol (entity.Id hariç)
            var uniqueErr = await EnsureUniqueTechnicianAsync(
                currentUserId: entity.Id,
                technicianCode: entity.TechnicianCode,
                technicianEmail: entity.TechnicianEmail,
                ct: CancellationToken.None // imzaya ct eklersen ct ver
            );
            if (uniqueErr != null)
                return uniqueErr;

            // audit + save
            await SetUpdateAuditIfExists(entity);
            await _repo.CompleteAsync();

            // DTO dön
            var q = _repo.GetQueryable<User>();
            var inc = IncludeExpression();
            if (inc is not null) q = inc(q);

            var updated = await q.AsNoTracking()
                                 .Where(u => u.Id == entity.Id)
                                 .ProjectToType<UserGetDto>(_config)
                                 .FirstAsync();

            return ResponseModel<UserGetDto>.Success(updated, Messages.Updated);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ResponseModel<UserGetDto>.Fail(Messages.ConflictError, StatusCode.Conflict);
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
    public async Task<ResponseModel<UserGetDto>> SignInAsync(string identifier, string password)
    {

        // Find user by email and include roles
        var user = _unitOfWork.Repository.GetMultiple<User>(
            asNoTracking: false,
            whereExpression: u => u.TechnicianEmail == identifier || u.TechnicianCode == identifier,
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
            expires: DateTime.Now.AddMinutes(15),
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

        if (jwtToken.ValidTo < DateTime.Now)
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
    public async Task<ResponseModel<UserGetDto>> ChangePasswordWithOldAsync(
    long userId,
    string oldPassword,
    string newPassword,
    string newPasswordConfirm,
    CancellationToken cancellationToken = default)
    {
        // 0) Temel kontroller
        if (string.IsNullOrWhiteSpace(oldPassword) ||
            string.IsNullOrWhiteSpace(newPassword) ||
            string.IsNullOrWhiteSpace(newPasswordConfirm))
        {
            return ResponseModel<UserGetDto>.Fail(Messages.RecoveryOldNewRequired, StatusCode.BadRequest);
            // Örn: "Recovery code, old password, and new password are required." mesajını bu senaryoya uyarlayabilirsin
        }

        if (newPassword != newPasswordConfirm)
            return ResponseModel<UserGetDto>.Fail(Messages.NewPasswordAndConfirmationDoNotMatch, StatusCode.BadRequest);

        // (Opsiyonel) Minimum uzunluk vb. politikalar
        var pwdPolicyError = ValidatePasswordStrength(newPassword);
        if (!string.IsNullOrEmpty(pwdPolicyError))
            return ResponseModel<UserGetDto>.Fail(pwdPolicyError, StatusCode.BadRequest);

        // 1) Kullanıcıyı tracking açık çek
        var user = await _unitOfWork.Repository.GetSingleAsync<User>(
            asNoTracking: false,
            whereExpression: u => u.Id == userId);

        if (user is null)
            return ResponseModel<UserGetDto>.Fail(Messages.UserNotFound, StatusCode.NotFound);

        // 2) Eski şifreyi doğrula
        var verifyOld = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, oldPassword);
        if (verifyOld == PasswordVerificationResult.Failed)
            return ResponseModel<UserGetDto>.Fail(Messages.InvalidEmailOrPassword, StatusCode.BadRequest);
        // İstersen "Eski şifre hatalı" gibi ayrı bir sabit kullan

        // 3) Yeni şifre eskisi ile aynı mı? (Doğrulamanın doğru yolu)
        var newEqualsOld = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword);
        if (newEqualsOld != PasswordVerificationResult.Failed)
            return ResponseModel<UserGetDto>.Fail(Messages.NewPasswordCannotBeSameAsOld, StatusCode.BadRequest);

        try
        {
            // 4) Hashle ve kaydet
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

            _unitOfWork.Repository.Update(user);
            await _unitOfWork.Repository.CompleteAsync();

            // 5) Güncel DTO'yu include'larla projekte ederek döndür
            var query = _unitOfWork.Repository.GetQueryable<User>();
            var inc = IncludeExpression();
            if (inc is not null) query = inc(query);

            var dto = await query.AsNoTracking()
                                 .Where(u => u.Id == user.Id)
                                 .ProjectToType<UserGetDto>(_config)
                                 .FirstAsync(cancellationToken);

            return ResponseModel<UserGetDto>.Success(dto, Messages.PasswordChangedSuccessfully, StatusCode.Ok);
        }
        catch (DbUpdateException ex)
        {
            return ResponseModel<UserGetDto>.Fail($"{Messages.DatabaseError}: {ex.Message}", StatusCode.Conflict);
        }
        catch (Exception ex)
        {
            return ResponseModel<UserGetDto>.Fail($"{Messages.FailedToChangePassword}: {ex.Message}", StatusCode.Error);
        }
    }

    /// <summary>
    /// Opsiyonel: Basit parola politikası (uzunluk, rakam, harf vb.)
    /// İstersen kaldırabilir ya da şirket politikasına göre geliştirebilirsin.
    /// </summary>
    private static string? ValidatePasswordStrength(string password)
    {
        if (password.Length < 6) return "Şifre en az 6 karakter olmalıdır.";
        if (!password.Any(char.IsDigit)) return "Şifre en az bir rakam içermelidir.";
        if (!password.Any(char.IsLower)) return "Şifre en az bir küçük harf içermelidir.";
        if (!password.Any(char.IsUpper)) return "Şifre en az bir büyük harf içermelidir.";
        return null;
    }


    public async Task<ResponseModel<List<UserGetDto>>> GetUserByRoleAsync(long roleId)
    {
        try
        {
            var users = await _repo.GetQueryable<User>()
                .Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId))
                .AsNoTracking()
                .ProjectToType<UserGetDto>(_config)
                .ToListAsync();

            if (users == null || users.Count == 0)
                return ResponseModel<List<UserGetDto>>.Fail("Bu role ait kullanıcı bulunamadı.", StatusCode.NotFound);

            return ResponseModel<List<UserGetDto>>.Success(users);
        }
        catch (Exception ex)
        {
            return ResponseModel<List<UserGetDto>>.Fail(
                $"Kullanıcılar alınırken hata oluştu: {ex.Message}",
                StatusCode.Error);
        }
    }

    public async Task<ResponseModel<List<UserGetDto>>> GetTechniciansAsync()
    {
        try
        {
            var users = await _repo.GetQueryable<User>()
                .Where(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "TECHNICIAN"))
                .AsNoTracking()
                .ProjectToType<UserGetDto>(_config)
                .ToListAsync();

            if (users == null || users.Count == 0)
                return ResponseModel<List<UserGetDto>>.Fail("Teknisyen bulunamadı.", StatusCode.NotFound);

            return ResponseModel<List<UserGetDto>>.Success(users);
        }
        catch (Exception ex)
        {
            return ResponseModel<List<UserGetDto>>.Fail(
                $"Kullanıcılar alınırken hata oluştu: {ex.Message}",
                StatusCode.Error);
        }
    }


    public async Task<ResponseModel<UserGetDto>> UpdateUserPassword(long id, string newPassword, CancellationToken ct = default)
    {
        if (id <= 0)
            return ResponseModel<UserGetDto>.Fail(Messages.UserNotFound, StatusCode.NotFound);

        if (string.IsNullOrWhiteSpace(newPassword))
            return ResponseModel<UserGetDto>.Fail(Messages.NewPasswordTooShort, StatusCode.BadRequest);

        var pwdPolicyError = ValidatePasswordStrength(newPassword);
        if (!string.IsNullOrEmpty(pwdPolicyError))
            return ResponseModel<UserGetDto>.Fail(pwdPolicyError, StatusCode.BadRequest);

        // tracking açık al (update yapacağız)
        var user = await _unitOfWork.Repository.GetSingleAsync<User>(
            asNoTracking: false,
            whereExpression: u => u.Id == id);

        if (user is null)
            return ResponseModel<UserGetDto>.Fail(Messages.UserNotFound, StatusCode.NotFound);

        // Yeni şifre eskisiyle aynı mı? (doğru kontrol)
        var sameAsOld = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword);
        if (sameAsOld != PasswordVerificationResult.Failed)
            return ResponseModel<UserGetDto>.Fail(Messages.NewPasswordCannotBeSameAsOld, StatusCode.BadRequest);

        try
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

            _unitOfWork.Repository.Update(user);
            await _unitOfWork.Repository.CompleteAsync();

            // İstersen sadece success dönebilirsin; ben güncel DTO’yu döndürüyorum
            var query = _unitOfWork.Repository.GetQueryable<User>();
            var inc = IncludeExpression();
            if (inc is not null) query = inc(query);

            var dto = await query.AsNoTracking()
                                 .Where(u => u.Id == id)
                                 .ProjectToType<UserGetDto>(_config)
                                 .FirstAsync(ct);

            return ResponseModel<UserGetDto>.Success(dto, Messages.PasswordChangedSuccessfully, StatusCode.Ok);
        }
        catch (DbUpdateException ex)
        {
            return ResponseModel<UserGetDto>.Fail($"{Messages.DatabaseError}: {ex.Message}", StatusCode.Conflict);
        }
        catch (Exception ex)
        {
            return ResponseModel<UserGetDto>.Fail($"{Messages.FailedToChangePassword}: {ex.Message}", StatusCode.Error);
        }
    }

    //Ortak kontrol helper’ı
    private async Task<ResponseModel<UserGetDto>?> EnsureUniqueTechnicianAsync(
    long currentUserId,
    string? technicianCode,
    string? technicianEmail,
    CancellationToken ct)
    {
        var code = technicianCode?.Trim();
        var email = technicianEmail?.Trim();

        // ikisi de boşsa kontrol etme (istersen zorunlu yaparsın)
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(email))
            return null;

        // aynı anda tek sorguda kontrol (IgnoreQueryFilters istersen soft delete için)
        var exists = await _unitOfWork.Repository.GetQueryable<User>()
            .AsNoTracking()
            .Where(u => u.Id != currentUserId && !u.IsDeleted)
            .Where(u =>
                (!string.IsNullOrWhiteSpace(code) && u.TechnicianCode == code) ||
                (!string.IsNullOrWhiteSpace(email) && u.TechnicianEmail == email)
            )
            .Select(u => new { u.TechnicianCode, u.TechnicianEmail })
            .FirstOrDefaultAsync(ct);

        if (exists is null)
            return null;

        // hangi alan çakıştı?
        if (!string.IsNullOrWhiteSpace(code) && string.Equals(exists.TechnicianCode, code, StringComparison.OrdinalIgnoreCase))
            return ResponseModel<UserGetDto>.Fail("Aynı kullanıcı adı mevcut.", StatusCode.BadRequest);

        if (!string.IsNullOrWhiteSpace(email) && string.Equals(exists.TechnicianEmail, email, StringComparison.OrdinalIgnoreCase))
            return ResponseModel<UserGetDto>.Fail("Aynı email  mevcut.", StatusCode.BadRequest);

        return ResponseModel<UserGetDto>.Fail("Aynı kullanıcı adı veya email mevcut.", StatusCode.BadRequest);
    }

}
