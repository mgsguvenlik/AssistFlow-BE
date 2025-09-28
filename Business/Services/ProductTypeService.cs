using System.Linq.Expressions;
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.ProductType;

namespace Business.Services
{
    public class ProductTypeService
      : CrudServiceBase<ProductType, long, ProductTypeCreateDto, ProductTypeUpdateDto, ProductTypeGetDto>,
        IProductTypeService
    {
        public ProductTypeService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(ProductType e) => e.Id;
        protected override Expression<Func<ProductType, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<ProductType>, IIncludableQueryable<ProductType, object>>? IncludeExpression()
            => null;

        protected override Task<ProductType?> ResolveEntityForUpdateAsync(ProductTypeUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<ProductType>(false, x => x.Id == dto.Id);
    }
}
