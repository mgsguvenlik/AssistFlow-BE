// Business/Services/ModuleService.cs
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query;
using Model.Dtos.Menu;
using System.Linq.Expressions;
using ModuleEntity = Model.Concrete.Menu; // <-- Autofac.Module çakışmasına çözüm

public class MenuService
  : CrudServiceBase<ModuleEntity, long, MenuCreateDto, MenuUpdateDto, MenuGetDto>,
    IMenuService
{
    public MenuService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IHttpContextAccessor? http = null)
        : base(uow, mapper, config, http) { }

    protected override long ReadKey(ModuleEntity e) => e.Id;
    protected override Expression<Func<ModuleEntity, bool>> KeyPredicate(long id) => m => m.Id == id;

    // Listeleme / GetById'de Include lazımsa burada tanımlarsın (şimdilik yok)
    protected override Func<IQueryable<ModuleEntity>, IIncludableQueryable<ModuleEntity, object>>? IncludeExpression()
        => null;

    protected override async Task<ModuleEntity?> ResolveEntityForUpdateAsync(MenuUpdateDto dto)
    {
        if (dto.Id <= 0) return null;
        return await _unitOfWork.Repository.GetByIdAsync<ModuleEntity>(
            asNoTracking: false,
            id: dto.Id
        );
    }
}
