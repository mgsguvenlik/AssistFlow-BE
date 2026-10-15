using Core.Common;
using Model.Dtos.Dashboard;

namespace Business.Interfaces
{
    public interface IWorkFlowDashboardService
    {
        Task<ResponseModel<OperationDashboardDto>> GetOperationsAsync(string? tenantCode = null, DateOnly? from = null, DateOnly? to = null, long[]? serviceTypeIds = null);
        Task<ResponseModel<List<DashboardServiceTypeOptionDto>>> GetServiceTypeOptionsAsync();
        /// <summary>
        /// Ana KPI metriklerini getirir
        /// </summary>
        Task<ResponseModel<DashboardKpiDto>> GetKpiAsync(long[]? serviceTypeIds = null);

        /// <summary>
        /// Teknisyen performans istatistiklerini getirir
        /// </summary>
        Task<ResponseModel<List<TechnicianPerformanceDto>>> GetTechnicianPerformanceAsync(DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// En çok iş yapan müşterileri getirir
        /// </summary>
        Task<ResponseModel<List<CustomerStatisticsDto>>> GetTopCustomersAsync(int count = 10);

        /// <summary>
        /// Ürün kullanım istatistiklerini getirir
        /// </summary>
        Task<ResponseModel<ProductStatisticsDto>> GetProductStatisticsAsync(long[]? serviceTypeIds = null);

        /// <summary>
        /// Zaman bazlı trend analizini getirir
        /// </summary>
        Task<ResponseModel<TimeBasedTrendDto>> GetTrendAnalysisAsync(int days = 30);

        /// <summary>
        /// Adım bazlı süre analizini getirir
        /// </summary>
        Task<ResponseModel<List<StepDurationAnalysisDto>>> GetStepDurationAnalysisAsync();

        /// <summary>
        /// Finansal dashboard verilerini getirir
        /// </summary>
        Task<ResponseModel<FinancialDashboardDto>> GetFinancialDashboardAsync(long[]? serviceTypeIds = null);

        /// <summary>
        /// Kritik uyarıları getirir
        /// </summary>
        Task<ResponseModel<CriticalAlertsDto>> GetCriticalAlertsAsync(long[]? serviceTypeIds = null);

        /// <summary>
        /// Coğrafi dağılım istatistiklerini getirir
        /// </summary>
        Task<ResponseModel<GeographicDistributionDto>> GetGeographicDistributionAsync(long[]? serviceTypeIds = null);

        /// <summary>
        /// YKB 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        Task<ResponseModel<YkbDashboardKpiDto>> GetYkbKpiAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, long[]? serviceTypeIds = null);
        Task<ResponseModel<EkbDashboardKpiDto>> GetEkbKpiAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, long[]? serviceTypeIds = null);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task<ResponseModel<List<YkbTechnicalServiceStatusCountDto>>> YkbGetMyTechnicalServiceStatusCountsAsync();
        Task<ResponseModel<List<EkbTechnicalServiceStatusCountDto>>> EkbGetMyTechnicalServiceStatusCountsAsync();


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task<ResponseModel<List<TechnicalServiceStatusCountDto>>> GetMyTechnicalServiceStatusCountsAsync();


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task<ResponseModel<List<QnbTechnicalServiceStatusCountDto>>> QnbGetMyTechnicalServiceStatusCountsAsync();
    }
}
