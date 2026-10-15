using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.Ekb;
using Model.Concrete.Qnb;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Dtos.Dashboard;

namespace Business.Services;

public partial class WorkFlowDashboardService
{
    private static readonly (string Code, string Name, string Menu)[] OperationTenants =
    {
        ("individual", "Bireysel", DashboardMenuKeys.Service),
        ("ykb", "YKB", DashboardMenuKeys.Ykb),
        ("ekb", "Emlak Katılım", DashboardMenuKeys.Ekb),
        ("qnb", "QNB", DashboardMenuKeys.Qnb)
    };

    public async Task<ResponseModel<OperationDashboardDto>> GetOperationsAsync(
        string? tenantCode = null, DateOnly? from = null, DateOnly? to = null, long[]? serviceTypeIds = null)
    {
        if (from > to || from == DateOnly.MaxValue || to == DateOnly.MaxValue)
            return ResponseModel<OperationDashboardDto>.Fail("Geçerli bir tarih aralığı seçiniz.", StatusCode.BadRequest);

        var requested = tenantCode?.Trim().ToLowerInvariant();
        if (requested != null && !OperationTenants.Any(t => t.Code == requested))
            return ResponseModel<OperationDashboardDto>.Fail("Geçersiz dashboard.", StatusCode.BadRequest);

        try
        {
            var me = await _currentUser.GetAsync();
            if (me == null || me.Id <= 0)
                return ResponseModel<OperationDashboardDto>.Fail("Bu işlem için yetkiniz bulunmamaktadır.", StatusCode.Unauthorized);

            // Read current grants from the database; client-supplied tenant lists never determine scope.
            var roles = _uow.Repository.GetQueryable<UserRole>()
                .Where(r => r.UserId == me.Id)
                .Join(_uow.Repository.GetQueryable<Role>().Where(r => !r.IsDeleted),
                    ur => ur.RoleId, r => r.Id, (ur, r) => r);
            if (requested == null && !await roles.AnyAsync(r => r.Code != null && r.Code.ToUpper() == "ADMIN"))
                return ResponseModel<OperationDashboardDto>.Fail("Bu işlem için yetkiniz bulunmamaktadır.", StatusCode.Unauthorized);

            var grantedMenus = await _uow.Repository.GetQueryable<MenuRole>()
                .Where(mr => mr.HasView && roles.Any(r => r.Id == mr.RoleId))
                .Join(_uow.Repository.GetQueryable<Menu>(), mr => mr.MenuId, m => m.Id, (mr, m) => m.Name)
                .ToListAsync();
            var allowed = OperationTenants.Where(t => grantedMenus.Contains(t.Menu, StringComparer.OrdinalIgnoreCase)
                && (requested == null || requested == t.Code)).ToArray();
            if (allowed.Length == 0)
                return ResponseModel<OperationDashboardDto>.Fail("Bu dashboard için yetkiniz bulunmamaktadır.", StatusCode.Unauthorized);

            var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            DateTimeOffset? start = from.HasValue
                ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(from.Value.ToDateTime(TimeOnly.MinValue), zone)) : null;
            DateTimeOffset? end = to.HasValue
                ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), zone)) : null;
            var result = new OperationDashboardDto();
            // These queries share a DbContext and must run sequentially.
            foreach (var tenant in allowed)
            {
                var query = FilterDashboardRows(OperationRows(tenant.Code), tenant.Code, serviceTypeIds);
                if (start.HasValue) query = query.Where(w => w.CreatedDate >= start.Value);
                if (end.HasValue) query = query.Where(w => w.CreatedDate < end.Value);
                var kpi = await query.GroupBy(w => 1).Select(g => new OperationDashboardKpiDto
                {
                    TotalWorkFlows = g.Count(),
                    CompletedWorkFlows = g.Count(w => w.Status == WorkFlowStatus.Complated),
                    PendingWorkFlows = g.Count(w => w.Status == WorkFlowStatus.Pending),
                    CancelledWorkFlows = g.Count(w => w.Status == WorkFlowStatus.Cancelled),
                    InServiceRequest = g.Count(w => w.Status == WorkFlowStatus.Pending && w.StepCode == "SR"),
                    InWarehouse = g.Count(w => w.Status == WorkFlowStatus.Pending && w.StepCode == "WH"),
                    InTechnicalService = g.Count(w => w.Status == WorkFlowStatus.Pending && w.StepCode == "TS"),
                    InPricing = g.Count(w => w.Status == WorkFlowStatus.Pending && w.StepCode == "PRC"),
                    InFinalApproval = g.Count(w => w.Status == WorkFlowStatus.Pending && w.StepCode == "APR"),
                    InCustomerApproval = g.Count(w => w.Status == WorkFlowStatus.Pending && w.StepCode == "CAPR")
                }).FirstOrDefaultAsync() ?? new();
                kpi.ServiceTypeDistribution = await GetStageServiceTypeCountsAsync(tenant.Code, query, pendingOnly: true);
                result.Tenants.Add(new() { TenantCode = tenant.Code, TenantName = tenant.Name, Kpi = kpi });
                AddOperations(result.Total, kpi);
            }
            return ResponseModel<OperationDashboardDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOperationsAsync failed for {TenantCode}", requested);
            return ResponseModel<OperationDashboardDto>.Fail("Dashboard verileri alınamadı.", StatusCode.Error);
        }
    }

    private sealed class OperationRow
    {
        public string RequestNo { get; set; } = string.Empty;
        public DateTimeOffset CreatedDate { get; set; }
        public WorkFlowStatus Status { get; set; }
        public string? StepCode { get; set; }
        public long? ServiceTypeId { get; set; }
    }

    private IQueryable<OperationRow> OperationRows(string code) => code switch
    {
        "individual" => _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
            .Select(w => new OperationRow { RequestNo = w.RequestNo, CreatedDate = w.CreatedDate, Status = w.WorkFlowStatus, StepCode = w.CurrentStep == null ? null : w.CurrentStep.Code }),
        "ykb" => _uow.Repository.GetQueryable<YkbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
            .Select(w => new OperationRow { RequestNo = w.RequestNo, CreatedDate = w.CreatedDate, Status = w.WorkFlowStatus, StepCode = w.CurrentStep == null ? null : w.CurrentStep.Code }),
        "ekb" => _uow.Repository.GetQueryable<EkbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
            .Select(w => new OperationRow { RequestNo = w.RequestNo, CreatedDate = w.CreatedDate, Status = w.WorkFlowStatus, StepCode = w.CurrentStep == null ? null : w.CurrentStep.Code }),
        "qnb" => _uow.Repository.GetQueryable<QnbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
            .Select(w => new OperationRow { RequestNo = w.RequestNo, CreatedDate = w.CreatedDate, Status = w.WorkFlowStatus, StepCode = w.CurrentStep == null ? null : w.CurrentStep.Code }),
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    private static void AddOperations(OperationDashboardKpiDto total, OperationDashboardKpiDto kpi)
    {
        foreach (var item in kpi.ServiceTypeDistribution)
        {
            var existing = total.ServiceTypeDistribution.FirstOrDefault(t =>
                t.StageCode == item.StageCode && t.ServiceTypeId == item.ServiceTypeId);
            if (existing != null) existing.Count += item.Count;
            else total.ServiceTypeDistribution.Add(new StageServiceTypeCountDto
            {
                StageCode = item.StageCode, ServiceTypeId = item.ServiceTypeId,
                ServiceTypeName = item.ServiceTypeName, Count = item.Count
            });
        }
        total.TotalWorkFlows += kpi.TotalWorkFlows;
        total.CompletedWorkFlows += kpi.CompletedWorkFlows;
        total.PendingWorkFlows += kpi.PendingWorkFlows;
        total.CancelledWorkFlows += kpi.CancelledWorkFlows;
        total.InServiceRequest += kpi.InServiceRequest;
        total.InWarehouse += kpi.InWarehouse;
        total.InTechnicalService += kpi.InTechnicalService;
        total.InPricing += kpi.InPricing;
        total.InFinalApproval += kpi.InFinalApproval;
        total.InCustomerApproval += kpi.InCustomerApproval;
    }
}
