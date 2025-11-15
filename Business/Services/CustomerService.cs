using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
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
          .Include(c => c.CustomerGroup)
          .Include(c => c.CustomerSystems);

        protected override Task<Customer?> ResolveEntityForUpdateAsync(CustomerUpdateDto dto)
          => _unitOfWork.Repository.GetSingleAsync<Customer>(
            asNoTracking: false,
            x => x.Id == dto.Id,
            includeExpression: q => q
                .Include(c => c.CustomerType)
                .Include(c => c.CustomerGroup)
                .Include(c => c.CustomerSystems) // 🔹 Update’te ilişkiyi yönetebilmek için
        );

        public override async Task<ResponseModel<CustomerGetDto>> UpdateAsync(CustomerUpdateDto dto)
        {
            var response = new ResponseModel<CustomerGetDto>();

            // 1) Entity’yi include’lu çek
            var entity = await ResolveEntityForUpdateAsync(dto);
            if (entity == null)
            {
                response.IsSuccess = false;          // kendi ResponseModel alanlarına göre düzelt
                response.Message = "Customer not found.";
                return response;
            }

            // 2) Scalar alanları map et (CustomerSystems Mapster config’inde ignore)
            _mapper.Map(dto, entity);

            // 3) SystemIds varsa many-to-many ilişkisini güncelle
            if (dto.SystemIds != null)
            {
                var systemsQuery = _unitOfWork.Repository.GetQueryable<CustomerSystem>();

                var systems = await systemsQuery
                    .Where(s => dto.SystemIds.Contains(s.Id))
                    .ToListAsync();

                entity.CustomerSystems ??= new List<CustomerSystem>();

                entity.CustomerSystems.Clear();
                foreach (var sys in systems)
                {
                    entity.CustomerSystems.Add(sys);
                }
            }

            // 4) Kaydet
            await _unitOfWork.Repository.CompleteAsync();

            // 5) DTO’ya map et ve ResponseModel ile dön
            var resultDto = _mapper.Map<CustomerGetDto>(entity);

            response.IsSuccess = true;               // kendi ResponseModel yapına göre
            response.Data = resultDto;
            response.Message = "Customer updated successfully.";

            return response;
        }

        public override async Task<ResponseModel<CustomerGetDto>> CreateAsync(CustomerCreateDto dto)
        {
            var response = new ResponseModel<CustomerGetDto>();

            var entity = _mapper.Map<Customer>(dto);

            // Create sırasında da sistem ataması yap
            if (dto.SystemIds != null && dto.SystemIds.Any())
            {
                var systems = await _unitOfWork.Repository
                    .GetQueryable<CustomerSystem>()
                    .Where(s => dto.SystemIds.Contains(s.Id))
                    .ToListAsync();

                entity.CustomerSystems = systems;
            }

            await _unitOfWork.Repository.AddAsync(entity);
            await _unitOfWork.Repository.CompleteAsync();

            var resultDto = _mapper.Map<CustomerGetDto>(entity);

            response.IsSuccess = true;
            response.Data = resultDto;
            response.Message = "Customer created successfully.";

            return response;
        }
    }
}
