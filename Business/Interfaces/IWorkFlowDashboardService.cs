using Core.Common;
using Model.Dtos.Dashboard;

namespace Business.Interfaces
{
    public interface IWorkFlowDashboardService
    {
        /// <summary>
        /// Ana KPI metriklerini getirir
        /// </summary>
        Task<ResponseModel<DashboardKpiDto>> GetKpiAsync();

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
        Task<ResponseModel<ProductStatisticsDto>> GetProductStatisticsAsync();

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
        Task<ResponseModel<FinancialDashboardDto>> GetFinancialDashboardAsync();

        /// <summary>
        /// Kritik uyarıları getirir
        /// </summary>
        Task<ResponseModel<CriticalAlertsDto>> GetCriticalAlertsAsync();

        /// <summary>
        /// Coğrafi dağılım istatistiklerini getirir
        /// </summary>
        Task<ResponseModel<GeographicDistributionDto>> GetGeographicDistributionAsync();

        /// <summary>
        /// YKB 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        Task<ResponseModel<YkbDashboardKpiDto>> GetYkbKpiAsync(DateTimeOffset? from = null, DateTimeOffset? to = null);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task<ResponseModel<List<YkbTechnicalServiceStatusCountDto>>> YkbGetMyTechnicalServiceStatusCountsAsync();


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
