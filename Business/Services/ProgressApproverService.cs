using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.ProgressApprover;
using System.Linq.Expressions;

namespace Business.Services
{
    public class ProgressApproverService
      : CrudServiceBase<ProgressApprover, long, ProgressApproverCreateDto, ProgressApproverUpdateDto, ProgressApproverGetDto>,
        IProgressApproverService
    {
        public ProgressApproverService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(ProgressApprover e) => e.Id;
        protected override Expression<Func<ProgressApprover, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<ProgressApprover>, IIncludableQueryable<ProgressApprover, object>>? IncludeExpression()
            => q => q.Include(p => p.CustomerGroup);

        protected override Task<ProgressApprover?> ResolveEntityForUpdateAsync(ProgressApproverUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<ProgressApprover>(false, x => x.Id == dto.Id,
                   q => q.Include(p => p.CustomerGroup));

        public async Task<ResponseModel<List<ProgressApproverGetDto>>> GetByCustomerIdAsync(long customerGroupId, CancellationToken cancellationToken)
        {
            var entities = await _unitOfWork.Repository.GetMultipleAsync<ProgressApprover>(false,
                x => x.CustomerGroupId == customerGroupId,             
                cancellationToken);
            var dtos = entities.Adapt<List<ProgressApproverGetDto>>(_config);
            return ResponseModel<List<ProgressApproverGetDto>>.Success(dtos);
        }
    }
}
