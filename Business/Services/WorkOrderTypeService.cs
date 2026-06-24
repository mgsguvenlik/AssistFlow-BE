using Business.Interfaces;
using Business.Interfaces.Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.WorkOrderType;
using System.Linq.Expressions;

namespace Business.Services
{
    public class WorkOrderTypeService
        : CrudServiceBase<
            WorkOrderType,
            long,
            WorkOrderTypeCreateDto,
            WorkOrderTypeUpdateDto,
            WorkOrderTypeGetDto>,
          IWorkOrderTypeService
    {
        public WorkOrderTypeService(
            IUnitOfWork uow,
            IMapper mapper,
            TypeAdapterConfig config)
            : base(uow, mapper, config)
        {
        }

        protected override long ReadKey(WorkOrderType entity)
            => entity.Id;

        protected override Expression<Func<WorkOrderType, bool>> KeyPredicate(long id)
            => x => x.Id == id;

        protected override Func<
            IQueryable<WorkOrderType>,
            IIncludableQueryable<WorkOrderType, object>
        >? IncludeExpression()
            => null;

        protected override Task<WorkOrderType?> ResolveEntityForUpdateAsync(
            WorkOrderTypeUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<WorkOrderType>(
                false,
                x => x.Id == dto.Id
            );
    }
}