using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Dtos.Model;
using System.Linq.Expressions;

namespace Business.Services
{
    public class ModelService
      : CrudServiceBase<Model.Concrete.Model, long, ModelCreateDto, ModelUpdateDto, ModelGetDto>,
        IModelService
    {
        public ModelService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(Model.Concrete.Model e) => e.Id;
        protected override Expression<Func<Model.Concrete.Model, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<Model.Concrete.Model>, IIncludableQueryable<Model.Concrete.Model, object>>? IncludeExpression()
            => q => q.Include(m => m.Brand);

        protected override Task<Model.Concrete.Model?> ResolveEntityForUpdateAsync(ModelUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<Model.Concrete.Model>(false, x => x.Id == dto.Id,
                   q => q.Include(m => m.Brand));
    }
}
