using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Model.Concrete;
using Model.Dtos.Configuration;
using System.Linq.Expressions;

namespace Business.Services
{
    public class ConfigurationService : CrudServiceBase<Configuration, long, ConfigurationCreateDto, ConfigurationUpdateDto, ConfigurationGetDto>,
        IConfigurationService
    {
        public ConfigurationService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }
        protected override long ReadKey(Configuration e) => e.Id;
        protected override Expression<Func<Configuration, bool>> KeyPredicate(long id) => b => b.Id == id;
        protected override async Task<Configuration?> ResolveEntityForUpdateAsync(ConfigurationUpdateDto dto)
        {
            if (dto.Id <= 0) return null;
            // 1) PK meta-cast ile güvenli getirme (include + theninclude)
            var entity = await _unitOfWork.Repository.GetByIdAsync<Configuration>(
                asNoTracking: false,
                id: dto.Id
            );
            if (entity != null) return entity;
            else return null;
        }
    }
}
