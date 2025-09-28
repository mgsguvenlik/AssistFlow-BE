using System.Linq.Expressions;
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.ServiceType;

namespace Business.Services
{
    public class ServiceTypeService
      : CrudServiceBase<ServiceType, long, ServiceTypeCreateDto, ServiceTypeUpdateDto, ServiceTypeGetDto>,
        IServiceTypeService
    {
        public ServiceTypeService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(ServiceType e) => e.Id;
        protected override Expression<Func<ServiceType, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<ServiceType>, IIncludableQueryable<ServiceType, object>>? IncludeExpression()
            => null;

        protected override Task<ServiceType?> ResolveEntityForUpdateAsync(ServiceTypeUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<ServiceType>(false, x => x.Id == dto.Id);
    }
}
