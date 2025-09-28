// Business.Services
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
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

    protected override Task<Brand?> ResolveEntityForUpdateAsync(BrandUpdateDto dto)
    {
        throw new NotImplementedException();
    }
}
