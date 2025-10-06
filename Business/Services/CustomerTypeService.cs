using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.CustomerType;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CustomerTypeService
      : CrudServiceBase<CustomerType, long, CustomerTypeCreateDto, CustomerTypeUpdateDto, CustomerTypeGetDto>,
        ICustomerTypeService
    {
        public CustomerTypeService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(CustomerType e) => e.Id;
        protected override Expression<Func<CustomerType, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<CustomerType>, IIncludableQueryable<CustomerType, object>>? IncludeExpression()
            => null;

        protected override Task<CustomerType?> ResolveEntityForUpdateAsync(CustomerTypeUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<CustomerType>(false, x => x.Id == dto.Id);
    }
}
