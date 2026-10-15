using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Concrete.Ekb;
using Model.Concrete.Qnb;
using Model.Dtos.Dashboard;

namespace Business.Services;

public partial class WorkFlowDashboardService
{
    private sealed class ServiceTypeSourceRow
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public long? ServiceTypeId { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
    }

    private IQueryable<ServiceTypeSourceRow> ServiceTypeSources(string code, bool technical) => (code, technical) switch
    {
        ("individual", true) => _uow.Repository.GetQueryable<TechnicalService>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        ("individual", false) => _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        ("ykb", true) => _uow.Repository.GetQueryable<YkbTechnicalService>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        ("ykb", false) => _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        ("ekb", true) => _uow.Repository.GetQueryable<EkbTechnicalService>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        ("ekb", false) => _uow.Repository.GetQueryable<EkbServicesRequest>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        ("qnb", true) => _uow.Repository.GetQueryable<QnbTechnicalService>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        ("qnb", false) => _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking()
            .Where(s => !s.IsDeleted).Select(s => new ServiceTypeSourceRow
            {
                Id = s.Id, RequestNo = s.RequestNo, ServiceTypeId = s.ServiceTypeId,
                CreatedDate = s.CreatedDate, UpdatedDate = s.UpdatedDate
            }),
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    public async Task<Core.Common.ResponseModel<List<DashboardServiceTypeOptionDto>>> GetServiceTypeOptionsAsync()
    {
        var options = await _uow.Repository.GetQueryable<ServiceType>().AsNoTracking()
            .OrderBy(t => t.Name).Select(t => new DashboardServiceTypeOptionDto { Id = t.Id, Name = t.Name }).ToListAsync();
        options.Add(new DashboardServiceTypeOptionDto { Id = 0, Name = "Belirtilmemiş" });
        return Core.Common.ResponseModel<List<DashboardServiceTypeOptionDto>>.Success(options);
    }

    private IQueryable<T> FilterDashboardRows<T>(IQueryable<T> query, string code, long[]? serviceTypeIds) where T : class
    {
        if (serviceTypeIds == null || serviceTypeIds.Length == 0) return query;
        var ids = serviceTypeIds.Distinct().ToArray();
        var matchingRequests = ResolveServiceTypes(code, OperationRows(code))
            .Where(w => ids.Contains(w.ServiceTypeId ?? 0)).Select(w => w.RequestNo);
        return query.Where(row => matchingRequests.Contains(EF.Property<string>(row, "RequestNo")));
    }

    private IQueryable<OperationRow> ResolveServiceTypes(string code, IQueryable<OperationRow> workflows)
    {
        var technical = ServiceTypeSources(code, technical: true);
        var requests = ServiceTypeSources(code, technical: false);

        // Select one latest record per request; keep workflows with missing relations.
        // A null technical type falls back to the originally requested type.
        return from w in workflows
                    from ts in technical.Where(s => s.RequestNo == w.RequestNo)
                        .OrderByDescending(s => s.UpdatedDate ?? s.CreatedDate).ThenByDescending(s => s.Id)
                        .Take(1).DefaultIfEmpty()
                    from sr in requests.Where(s => s.RequestNo == w.RequestNo)
                        .OrderByDescending(s => s.UpdatedDate ?? s.CreatedDate).ThenByDescending(s => s.Id)
                        .Take(1).DefaultIfEmpty()
                    select new OperationRow
                    {
                        RequestNo = w.RequestNo, CreatedDate = w.CreatedDate, Status = w.Status,
                        StepCode = w.StepCode, ServiceTypeId = ts.ServiceTypeId ?? sr.ServiceTypeId
                    };
    }

    private async Task<List<StageServiceTypeCountDto>> GetStageServiceTypeCountsAsync(
        string code, IQueryable<OperationRow> workflows, bool pendingOnly)
    {
        var stages = new[] { "SR", "WH", "TS", "PRC", "APR", "CAPR" };
        workflows = workflows.Where(w => stages.Contains(w.StepCode!));
        if (pendingOnly) workflows = workflows.Where(w => w.Status == WorkFlowStatus.Pending);
        var types = ResolveServiceTypes(code, workflows);
        var counts = await types.GroupBy(t => new { t.StepCode, t.ServiceTypeId })
            .Select(g => new StageServiceTypeCountDto
            {
                StageCode = g.Key.StepCode!, ServiceTypeId = g.Key.ServiceTypeId, Count = g.Count()
            }).ToListAsync();
        var ids = counts.Where(c => c.ServiceTypeId.HasValue).Select(c => c.ServiceTypeId!.Value).Distinct().ToArray();
        // Retain historical names even when a service type is no longer selectable.
        var names = await _uow.Repository.GetQueryable<ServiceType>().AsNoTracking()
            .Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
        foreach (var count in counts)
            count.ServiceTypeName = count.ServiceTypeId.HasValue && names.TryGetValue(count.ServiceTypeId.Value, out var name)
                && !string.IsNullOrWhiteSpace(name) ? name : "Belirtilmemiş";
        return counts;
    }
}
