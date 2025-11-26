using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Model.Concrete;
using Model.Dtos.CustomerSystem;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CustomerSystemService
      : CrudServiceBase<CustomerSystem, long, CustomerSystemCreateDto, CustomerSystemUpdateDto, CustomerSystemGetDto>,
        ICustomerSystemService
    {
        public CustomerSystemService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config)
        {
        }

        protected override long ReadKey(CustomerSystem e) => e.Id;

        protected override Expression<Func<CustomerSystem, bool>> KeyPredicate(long id)
            => cs => cs.Id == id;

        protected override async Task<CustomerSystem?> ResolveEntityForUpdateAsync(CustomerSystemUpdateDto dto)
        {
            if (dto.Id <= 0) return null;

            // BrandService’teki gibi, ama include yok; sade GetByIdAsync yeterli
            var entity = await _unitOfWork.Repository.GetByIdAsync<CustomerSystem>(
                asNoTracking: false,
                id: dto.Id
            );

            return entity;
        }
    }
}
