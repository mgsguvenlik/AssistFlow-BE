using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Dtos.CustomerSystemAssignment;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CustomerSystemAssignmentService
        : CrudServiceBase<CustomerSystemAssignment, long,
                          CustomerSystemAssignmentCreateDto,
                          CustomerSystemAssignmentUpdateDto,
                          CustomerSystemAssignmentGetDto>,
          ICustomerSystemAssignmentService
    {

        public CustomerSystemAssignmentService(
            IUnitOfWork uow,
            IMapper mapper,

            TypeAdapterConfig config)
            : base(uow, mapper, config)
        {
        }

        protected override long ReadKey(CustomerSystemAssignment e) => e.Id;

        protected override Expression<Func<CustomerSystemAssignment, bool>> KeyPredicate(long id)
            => x => x.Id == id;

        protected override async Task<CustomerSystemAssignment?> ResolveEntityForUpdateAsync(
            CustomerSystemAssignmentUpdateDto dto)
        {
            if (dto.Id <= 0)
                return null;

            var entity = await _unitOfWork.Repository.GetByIdAsync<CustomerSystemAssignment>(
                asNoTracking: false,
                id: dto.Id
            );

            return entity;
        }

        public async Task<List<CustomerSystemAssignmentGetDto>> GetByCustomerIdAsync(long customerId)
        {
            var q = _unitOfWork.Repository
                .GetQueryable<CustomerSystemAssignment>()
                .Where(x => x.CustomerId == customerId)
                .Include(x => x.Customer)
                .Include(x => x.CustomerSystem);


            return await q
                    .ProjectToType<CustomerSystemAssignmentGetDto>(_config)
                    .ToListAsync();
          
        }
    }
}
