using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.CustomerGroupProductPrice;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CustomerGroupProductPriceService :
      CrudServiceBase<CustomerGroupProductPrice, long,
                      CustomerGroupProductPriceCreateDto, CustomerGroupProductPriceUpdateDto, CustomerGroupProductPriceGetDto>,
      ICustomerGroupProductPriceService
    {
        public CustomerGroupProductPriceService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(CustomerGroupProductPrice e) => e.Id;

        protected override Expression<Func<CustomerGroupProductPrice, bool>> KeyPredicate(long id)
            => e => e.Id == id;

        // GET list/detail'de Group & Product çek
        protected override Func<IQueryable<CustomerGroupProductPrice>, IIncludableQueryable<CustomerGroupProductPrice, object>>?
            IncludeExpression()
            => q => q
                .Include(x => x.CustomerGroup)
                .Include(x => x.Product);

        protected override async Task<CustomerGroupProductPrice?> ResolveEntityForUpdateAsync(CustomerGroupProductPriceUpdateDto dto)
            => await _unitOfWork.Repository.GetSingleAsync<CustomerGroupProductPrice>(
                    asNoTracking: false,
                     x => x.Id == dto.Id,
                     q => q.Include(x => x.CustomerGroup)
                                   .Include(x => x.Product)
               );


        public async Task<List<CustomerGroupProductPriceGetDto>> GetByProductAndCustomerGroupAsync(long productId, long customerGroupId)
        {
            var entities = await _unitOfWork.Repository
                .GetQueryable<CustomerGroupProductPrice>()
                .Include(x => x.Product)
                .Include(x => x.CustomerGroup)
                .Where(x => x.ProductId == productId && x.CustomerGroupId == customerGroupId)
                .ToListAsync();

            return entities.Adapt<List<CustomerGroupProductPriceGetDto>>(_config);
        }
    }
}
