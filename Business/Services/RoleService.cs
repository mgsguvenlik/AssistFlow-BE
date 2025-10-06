using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.Role;
using System.Linq.Expressions;

namespace Business.Services
{
    public class RoleService : CrudServiceBase<Role, long, RoleCreateDto, RoleUpdateDto, RoleGetDto>, IRoleService
    {
        public RoleService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config)
        {
        }

        // Entity'nin anahtarını nasıl okuyacağımız
        protected override long ReadKey(Role entity) => entity.Id;

        // EF'in SQL'e çevirebileceği Id filtresi
        protected override Expression<Func<Role, bool>> KeyPredicate(long id)
            => r => r.Id == id;

        // Update işleminde takipli entity'yi getir (include gerekirse buraya ekleyebilirsin)
        protected override async Task<Role?> ResolveEntityForUpdateAsync(RoleUpdateDto dto)
            => await _unitOfWork.Repository.GetSingleAsync<Role>(asNoTracking: false, r => r.Id == dto.Id, includeExpression: r => r.Include(u => u.UserRoles).ThenInclude(u => u.User));

        // İlişki include ihtiyacın yoksa base'in IncludeExpression()'ını kullanma; boş bırakmak yeterli
        // Eğer ileride Role -> UserRoles gibi include isterse:
        protected override Func<IQueryable<Role>, IIncludableQueryable<Role, object>>? IncludeExpression()
            => q => q.Include(r => r.UserRoles).ThenInclude(x => x.User);
    }
}
