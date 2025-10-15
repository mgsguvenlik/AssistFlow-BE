using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.CustomerGroup;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CustomerGroupService
        : CrudServiceBase<CustomerGroup, long, CustomerGroupCreateDto, CustomerGroupUpdateDto, CustomerGroupGetDto>, ICustomerGroupService
    {
        public CustomerGroupService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IHttpContextAccessor http)
            : base(uow, mapper, config, http)
        {
        }

        protected override long ReadKey(CustomerGroup entity) => entity.Id;

        protected override Expression<Func<CustomerGroup, bool>> KeyPredicate(long id)
            => x => x.Id == id;

        protected override async Task<CustomerGroup?> ResolveEntityForUpdateAsync(CustomerGroupUpdateDto dto)
        {
            return await _repo.GetSingleAsync<CustomerGroup>(false, x => x.Id == dto.Id,
                include => include
                    .Include(x => x.ProgressApprovers)
                    .Include(x => x.SubGroups)
                    .Include(x => x.GroupProductPrices));
        }

        protected override Func<IQueryable<CustomerGroup>, IIncludableQueryable<CustomerGroup, object>>? IncludeExpression()
        {
            return q => q
                .Include(x => x.SubGroups)
                .Include(x => x.ParentGroup)
                .Include(x => x.GroupProductPrices)
                .Include(x => x.ProgressApprovers);
        }
    }
}
