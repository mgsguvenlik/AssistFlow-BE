// Business/Services/MenuRoleService.cs
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.MenuRole;
using System.Linq.Expressions;

public class MenuRoleService
  : CrudServiceBase<MenuRole, long, MenuRoleCreateDto, MenuRoleUpdateDto, MenuRoleGetDto>,
    IMenuRoleService
{
    public MenuRoleService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IHttpContextAccessor? http = null)
        : base(uow, mapper, config, http) { }

    protected override long ReadKey(MenuRole e) => e.Id;
    protected override Expression<Func<MenuRole, bool>> KeyPredicate(long id) => x => x.Id == id;

    // >>> HATA: IncludeForGet yoktu, doğru olan IncludeExpression() (CrudServiceBase'te var)
    protected override Func<IQueryable<MenuRole>, IIncludableQueryable<MenuRole, object>>? IncludeExpression()
        => q => q
            .Include(x => x.Menu)
            .Include(x => x.Role);

    protected override async Task<MenuRole?> ResolveEntityForUpdateAsync(MenuRoleUpdateDto dto)
    {
        if (dto.Id <= 0) return null;

        return await _unitOfWork.Repository.GetByIdAsync<MenuRole>(
            asNoTracking: false,
            id: dto.Id,
            includeExpression: q => q
                .Include(x => x.Menu)
                .Include(x => x.Role)
        );
    }

    // İsteğe bağlı: duplicate korumasını servicede de yapabilirsin (unique index zaten var)
    public override async Task<ResponseModel<MenuRoleGetDto>> CreateAsync(MenuRoleCreateDto dto)
    {
        var exists = await _unitOfWork.Repository.AnyAsync<MenuRole>(
            x => x.MenuId == dto.MenuId && x.RoleId == dto.RoleId);

        if (exists)
            return ResponseModel<MenuRoleGetDto>.Fail("Bu rol için ilgili modül zaten yetkilendirilmiş.", StatusCode.Conflict);

        return await base.CreateAsync(dto);
    }

    // Kolay filtreler
    public async Task<ResponseModel<List<MenuRoleGetDto>>> GetByRoleIdAsync(long roleId)
    {
        var q = _unitOfWork.Repository.GetQueryable<MenuRole>();
        q = IncludeExpression()!(q);
        var data = await q.AsNoTracking()
                      .Where(x => x.RoleId == roleId)
                      .ProjectToType<MenuRoleGetDto>(_config)
                      .ToListAsync();

        return ResponseModel<List<MenuRoleGetDto>>.Success(data, "", StatusCode.Ok);
    }

    public async Task<IReadOnlyList<MenuRoleGetDto>> GetByMenuIdAsync(long menuId)
    {
        var q = _unitOfWork.Repository.GetQueryable<MenuRole>();
        q = IncludeExpression()!(q);
        return await q.AsNoTracking()
                      .Where(x => x.MenuId == menuId)
                      .ProjectToType<MenuRoleGetDto>(_config)
                      .ToListAsync();
    }

    public async Task<ResponseModel<List<MenuRoleGetDto>>> GetByUserIdAsync(long userId)
    {
        var userRoleIds = await _unitOfWork.Repository
            .GetQueryable<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (!userRoleIds.Any())
            return ResponseModel<List<MenuRoleGetDto>>.Success(new List<MenuRoleGetDto>());

        var q = _unitOfWork.Repository.GetQueryable<MenuRole>();
        q = IncludeExpression()!(q);

        var menuRoles = await q
            .AsNoTracking()
            .Where(mr => userRoleIds.Contains(mr.RoleId))
            .ProjectToType<MenuRoleGetDto>(_config)
            .ToListAsync();

        return ResponseModel<List<MenuRoleGetDto>>.Success(menuRoles, "", StatusCode.Ok);
    }
}
