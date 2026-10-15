namespace Model.Dtos.Dashboard;

public static class DashboardMenuKeys
{
    public const string Service = "ServiceDashboard";
    public const string Ykb = "YkbDashboard";
    public const string Ekb = "EkbDashboard";
    public const string Qnb = "QnbDashboard";
}

public sealed class OperationDashboardKpiDto
{
    public List<StageServiceTypeCountDto> ServiceTypeDistribution { get; set; } = new();

    public int TotalWorkFlows { get; set; }
    public int CompletedWorkFlows { get; set; }
    public int PendingWorkFlows { get; set; }
    public int CancelledWorkFlows { get; set; }
    public int InServiceRequest { get; set; }
    public int InWarehouse { get; set; }
    public int InTechnicalService { get; set; }
    public int InPricing { get; set; }
    public int InFinalApproval { get; set; }
    public int InCustomerApproval { get; set; }
}

public sealed class TenantOperationDashboardDto
{
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public OperationDashboardKpiDto Kpi { get; set; } = new();
}

public sealed class OperationDashboardDto
{
    public OperationDashboardKpiDto Total { get; set; } = new();
    public List<TenantOperationDashboardDto> Tenants { get; set; } = new();
}
