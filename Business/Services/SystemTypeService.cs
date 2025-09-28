using System.Linq.Expressions;
using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.SystemType;

namespace Business.Services
{
    public class SystemTypeService
      : CrudServiceBase<SystemType, long, SystemTypeCreateDto, SystemTypeUpdateDto, SystemTypeGetDto>,
        ISystemTypeService
    {
        public SystemTypeService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(SystemType e) => e.Id;

        protected override Expression<Func<SystemType, bool>> KeyPredicate(long id)
            => x => x.Id == id;

        // İlişki yoksa include gerekmez
        protected override Func<IQueryable<SystemType>, IIncludableQueryable<SystemType, object>>? IncludeExpression()
            => null;

        protected override Task<SystemType?> ResolveEntityForUpdateAsync(SystemTypeUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<SystemType>(asNoTracking: false, x => x.Id == dto.Id);
    }
}
