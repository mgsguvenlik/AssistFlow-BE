using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Dtos.Brand;
using System.Linq.Expressions;

public class BrandService
  : CrudServiceBase<Brand, long, BrandCreateDto, BrandUpdateDto, BrandGetDto>,
    IBrandService
{
    public BrandService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
        : base(uow, mapper, config) { }

    protected override long ReadKey(Brand e) => e.Id;
    protected override Expression<Func<Brand, bool>> KeyPredicate(long id) => b => b.Id == id;

    protected override async Task<Brand?> ResolveEntityForUpdateAsync(BrandUpdateDto dto)
    {
        if (dto.Id <= 0) return null;
        // 1) PK meta-cast ile güvenli getirme (include + theninclude)
        var entity = await _unitOfWork.Repository.GetByIdAsync<Brand>(
            asNoTracking: false,
            id: dto.Id,
            includeExpression: q => q
                .Include(u => u.Models)
        );

        if (entity != null) return entity;
        else return null;
    }
}
