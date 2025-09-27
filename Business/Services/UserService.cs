using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Microsoft.AspNet.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.User;
using System.Linq.Expressions;

public class UserService
    : CrudServiceBase<User, long, UserCreateDto, UserUpdateDto, UserGetDto>,IUserService
{
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IPasswordHasher<User> passwordHasher)
        : base(uow, mapper, config)
    {
        _passwordHasher = passwordHasher;
    }

    protected override long ReadKey(User entity) => entity.Id;

    protected override Expression<Func<User, bool>> KeyPredicate(long id)
        => u => u.Id == id;

    // Include'lar (roller)
    protected override Func<IQueryable<User>, IIncludableQueryable<User, object>>? IncludeExpression()
        => q => q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role);

    protected override async Task<User?> ResolveEntityForUpdateAsync(UserUpdateDto dto)
    {
        // tracked + include
        return await _unitOfWork.Repository
            .GetSingleAsync<User>(asNoTracking: false, u => u.Id == dto.Id,
                q => q.Include(u => u.UserRoles));
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
                return ResponseModel<UserGetDto>.Fail("Kullanıcı bulunamadı.", StatusCode.NotFound);

            // Var olan role’leri doğrula (geçersiz id varsa bildir)
            var existingRoles = await _unitOfWork.Repository.GetMultipleAsync<Role>(
                asNoTracking: true,
                r => desired.Contains(r.Id));

            var existingIds = existingRoles.Select(r => r.Id).ToHashSet();
            var missing = desired.Except(existingIds).ToList();
            if (missing.Any())
                return ResponseModel<UserGetDto>.Fail($"Bilinmeyen rol id(s): {string.Join(", ", missing)}", StatusCode.BadRequest);

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

            return ResponseModel<UserGetDto>.Success(dto, "Roller Güncelendi.");
        }
        catch (Exception ex)
        {
            return ResponseModel<UserGetDto>.Fail($"Beklenmedik hata: {ex.Message}", StatusCode.Error);
        }
    }
}
