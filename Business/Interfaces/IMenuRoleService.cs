using Model.Dtos.MenuRole;

namespace Business.Interfaces
{
    public interface IMenuRoleService
       : ICrudService<MenuRoleCreateDto,
                      MenuRoleUpdateDto,
                      MenuRoleGetDto,
                      long>
    {
        // İsteğe bağlı: Role’a göre modüller vb. özel sorgular
        Task<IReadOnlyList<Model.Dtos.MenuRole.MenuRoleGetDto>> GetByRoleIdAsync(long roleId);
        Task<IReadOnlyList<Model.Dtos.MenuRole.MenuRoleGetDto>> GetByModuleIdAsync(long moduleId);
    }
}
