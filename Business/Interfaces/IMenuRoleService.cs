using Core.Common;
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
        Task<ResponseModel<List<MenuRoleGetDto>>> GetByRoleIdAsync(long roleId);
        Task<IReadOnlyList<MenuRoleGetDto>> GetByMenuIdAsync(long menuId);
    }
}
