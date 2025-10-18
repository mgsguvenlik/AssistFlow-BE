using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.Customer;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CustomerService
      : CrudServiceBase<Customer, long, CustomerCreateDto, CustomerUpdateDto, CustomerGetDto>,
        ICustomerService
    {
        public CustomerService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(Customer e) => e.Id;
        protected override Expression<Func<Customer, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<Customer>, IIncludableQueryable<Customer, object>>? IncludeExpression()
            => q => q
            .Include(c => c.CustomerType)
            .Include(x=>x.CustomerGroup);

        protected override Task<Customer?> ResolveEntityForUpdateAsync(CustomerUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<Customer>(false, x => x.Id == dto.Id,
                   q => q.Include(c => c.CustomerType));
    }
}
