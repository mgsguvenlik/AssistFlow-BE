using System.Linq.Expressions;
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.Product;

namespace Business.Services
{
    public class ProductService
      : CrudServiceBase<Product, long, ProductCreateDto, ProductUpdateDto, ProductGetDto>,
        IProductService
    {
        public ProductService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(Product e) => e.Id;
        protected override Expression<Func<Product, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<Product>, IIncludableQueryable<Product, object>>? IncludeExpression()
            => q => q.Include(p => p.Brand)
                     .Include(p => p.Model)
                     .Include(p => p.CurrencyType)
                     .Include(p => p.ProductType);

        protected override Task<Product?> ResolveEntityForUpdateAsync(ProductUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<Product>(false, x => x.Id == dto.Id,
                   q => q.Include(p => p.Brand)
                         .Include(p => p.Model)
                         .Include(p => p.CurrencyType)
                         .Include(p => p.ProductType));
    }
}
