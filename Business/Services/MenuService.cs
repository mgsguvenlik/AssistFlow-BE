using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Dtos.Menu;
using System.Linq.Expressions;

public class MenuService
    : CrudServiceBase<Menu, long,
                      MenuCreateDto,
                      MenuUpdateDto,
                      MenuGetDto>,
      IMenuService
{
    public MenuService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IHttpContextAccessor? http = null)
        : base(uow, mapper, config, http) { }

    protected override long ReadKey(Menu e) => e.Id;
    protected override Expression<Func<Menu, bool>> KeyPredicate(long id) => m => m.Id == id;

    protected override Func<IQueryable<Menu>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Menu, object>>? IncludeExpression()
        => q => q.Include(x => x.MenuRoles);

    protected override Task<Menu?> ResolveEntityForUpdateAsync(MenuUpdateDto dto)
        => _repo.GetByIdAsync<Menu>(asNoTracking: false, id: dto.Id, includeExpression: q => q.Include(x => x.MenuRoles));

    public async Task<IReadOnlyList<MenuWithPermissionsDto>> GetByUserIdAsync(long userId)
    {
        if (userId <= 0) return new List<MenuWithPermissionsDto>();

        var roleIdsQ = _repo.GetQueryable<UserRole>()
                            .Where(ur => ur.UserId == userId)
                            .Select(ur => ur.RoleId);

        var query =
            from m in _repo.GetQueryable<Menu>()
            join mr in _repo.GetQueryable<MenuRole>() on m.Id equals mr.MenuId
            where roleIdsQ.Contains(mr.RoleId) && mr.HasView
            group mr by new { m.Id, m.Name, m.Description } into g
            select new MenuWithPermissionsDto
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                Description = g.Key.Description,
                CanView = g.Any(x => x.HasView),
                CanEdit = g.Any(x => x.HasEdit)
            };

        return await query.AsNoTracking()
                          .OrderBy(x => x.Name)
                          .ToListAsync();
    }
}
