using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.CustomerProductPrice;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CustomerProductPriceService :
      CrudServiceBase<CustomerProductPrice, long,
                      CustomerProductPriceCreateDto, CustomerProductPriceUpdateDto, CustomerProductPriceGetDto>,
      ICustomerProductPriceService
    {
        public CustomerProductPriceService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(CustomerProductPrice e) => e.Id;

        protected override Expression<Func<CustomerProductPrice, bool>> KeyPredicate(long id)
            => e => e.Id == id;

        // GET list/detail'de Customer & Product çek
        protected override Func<IQueryable<CustomerProductPrice>, IIncludableQueryable<CustomerProductPrice, object>>?
            IncludeExpression()
            => q => q
                .Include(x => x.Customer)
                .Include(x => x.Product);

        protected override async Task<CustomerProductPrice?> ResolveEntityForUpdateAsync(CustomerProductPriceUpdateDto dto)
            => await _unitOfWork.Repository.GetSingleAsync<CustomerProductPrice>(
                    asNoTracking: false,
                     x => x.Id == dto.Id,
                     q => q.Include(x => x.Customer)
                                   .Include(x => x.Product)
               );
    }

}
