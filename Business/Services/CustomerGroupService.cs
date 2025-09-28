using System.Linq.Expressions;
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.CustomerGroup;

namespace Business.Services
{
    public class CustomerGroupService
      : CrudServiceBase<CustomerGroup, long, CustomerGroupCreateDto, CustomerGroupUpdateDto, CustomerGroupGetDto>,
        ICustomerGroupService
    {
        public CustomerGroupService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(CustomerGroup e) => e.Id;
        protected override Expression<Func<CustomerGroup, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<CustomerGroup>, IIncludableQueryable<CustomerGroup, object>>? IncludeExpression()
            => null;

        protected override Task<CustomerGroup?> ResolveEntityForUpdateAsync(CustomerGroupUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<CustomerGroup>(false, x => x.Id == dto.Id);
    }
}
