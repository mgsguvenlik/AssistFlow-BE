using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Utilities.Security;
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
        public override Task<ResponseModel<ConfigurationGetDto>> CreateAsync(ConfigurationCreateDto dto)
        {
            if (dto.Name == PasswordPolicyRules.ValidityDaysParameter && !PasswordPolicyRules.ValidDays(dto.Value))
                return Task.FromResult(ResponseModel<ConfigurationGetDto>.Fail("Şifre geçerlilik süresi 1 ile 36500 arasında tam sayı olmalıdır."));
            return base.CreateAsync(dto);
        }
        public override Task<ResponseModel<ConfigurationGetDto>> UpdateAsync(ConfigurationUpdateDto dto)
        {
            if (dto.Name == PasswordPolicyRules.ValidityDaysParameter && !PasswordPolicyRules.ValidDays(dto.Value))
                return Task.FromResult(ResponseModel<ConfigurationGetDto>.Fail("Şifre geçerlilik süresi 1 ile 36500 arasında tam sayı olmalıdır."));
            return base.UpdateAsync(dto);
        }
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
