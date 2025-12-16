using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.WorkFlowTransition;
using System.Linq.Expressions;

namespace Business.Services
{
    public class WorkFlowTransitionService
        : CrudServiceBase<WorkFlowTransition, long, WorkFlowTransitionCreateDto, WorkFlowTransitionUpdateDto, WorkFlowTransitionGetDto>, IWorkFlowTransitionService
    {
        public WorkFlowTransitionService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config)
        {
        }

        protected override long ReadKey(WorkFlowTransition entity) => entity.Id;

        protected override Expression<Func<WorkFlowTransition, bool>> KeyPredicate(long id)
            => x => x.Id == id;

        protected override async Task<WorkFlowTransition?> ResolveEntityForUpdateAsync(WorkFlowTransitionUpdateDto dto)
        {
            return await _repo.GetSingleAsync<WorkFlowTransition>(
                asNoTracking: false,
                whereExpression: x => x.Id == dto.Id);
        }

        protected override Func<IQueryable<WorkFlowTransition>, IIncludableQueryable<WorkFlowTransition, object>>? IncludeExpression()
        {
            return q => q
                .Include(x => x.FromStep)
                .Include(x => x.ToStep);
        }
    }
}
