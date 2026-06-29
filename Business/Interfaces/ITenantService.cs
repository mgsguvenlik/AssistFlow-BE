using Model.Dtos.CustomerSystem;
using Model.Dtos.Tenant;

namespace Business.Interfaces
{
    public interface ITenantService : ICrudService<TenantCreateDto, TenantUpdateDto, TenantGetDto, long>
    {
    }
}
