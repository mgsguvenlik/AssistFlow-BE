// WebAPI\Controllers\DashboardController.cs
using Business.Interfaces;
using Business.Services;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Dashboard;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IWorkFlowDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IWorkFlowDashboardService dashboardService,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        /// <summary>
        /// Ana KPI metriklerini getirir
        /// </summary>
        /// <returns>Dashboard KPI verileri</returns>
        [HttpGet("kpi")]
        [ProducesResponseType(typeof(ResponseModel<DashboardKpiDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<DashboardKpiDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetKpi()
        {
            var result = await _dashboardService.GetKpiAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Teknisyen performans istatistiklerini getirir
        /// </summary>
        /// <param name="from">Başlangıç tarihi (opsiyonel)</param>
        /// <param name="to">Bitiş tarihi (opsiyonel)</param>
        /// <returns>Teknisyen performans listesi</returns>
        [HttpGet("technician-performance")]
        [ProducesResponseType(typeof(ResponseModel<List<TechnicianPerformanceDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<List<TechnicianPerformanceDto>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTechnicianPerformance(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var result = await _dashboardService.GetTechnicianPerformanceAsync(from, to);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// En çok iş yapan müşterileri getirir
        /// </summary>
        /// <param name="count">Getirilecek müşteri sayısı (varsayılan: 10)</param>
        /// <returns>Müşteri istatistikleri listesi</returns>
        [HttpGet("top-customers")]
        [ProducesResponseType(typeof(ResponseModel<List<CustomerStatisticsDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<List<CustomerStatisticsDto>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTopCustomers([FromQuery] int count = 10)
        {
            var result = await _dashboardService.GetTopCustomersAsync(count);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Ürün kullanım istatistiklerini getirir
        /// </summary>
        /// <returns>Ürün istatistikleri</returns>
        [HttpGet("product-statistics")]
        [ProducesResponseType(typeof(ResponseModel<ProductStatisticsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<ProductStatisticsDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductStatistics()
        {
            var result = await _dashboardService.GetProductStatisticsAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Zaman bazlı trend analizini getirir
        /// </summary>
        /// <param name="days">Kaç günlük trend (varsayılan: 30)</param>
        /// <returns>Trend analiz verileri</returns>
        [HttpGet("trend-analysis")]
        [ProducesResponseType(typeof(ResponseModel<TimeBasedTrendDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<TimeBasedTrendDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTrendAnalysis([FromQuery] int days = 30)
        {
            var result = await _dashboardService.GetTrendAnalysisAsync(days);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Adım bazlı süre analizini getirir
        /// </summary>
        /// <returns>Adım süre analiz verileri</returns>
        [HttpGet("step-duration-analysis")]
        [ProducesResponseType(typeof(ResponseModel<List<StepDurationAnalysisDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<List<StepDurationAnalysisDto>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStepDurationAnalysis()
        {
            var result = await _dashboardService.GetStepDurationAnalysisAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Finansal dashboard verilerini getirir
        /// </summary>
        /// <returns>Finansal dashboard verileri</returns>
        [HttpGet("financial")]
        [ProducesResponseType(typeof(ResponseModel<FinancialDashboardDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<FinancialDashboardDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetFinancialDashboard()
        {
            var result = await _dashboardService.GetFinancialDashboardAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Kritik uyarıları getirir
        /// </summary>
        /// <returns>Kritik uyarı verileri</returns>
        [HttpGet("critical-alerts")]
        [ProducesResponseType(typeof(ResponseModel<CriticalAlertsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<CriticalAlertsDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCriticalAlerts()
        {
            var result = await _dashboardService.GetCriticalAlertsAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Coğrafi dağılım istatistiklerini getirir
        /// </summary>
        /// <returns>Coğrafi dağılım verileri</returns>
        [HttpGet("geographic-distribution")]
        [ProducesResponseType(typeof(ResponseModel<GeographicDistributionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<GeographicDistributionDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetGeographicDistribution()
        {
            var result = await _dashboardService.GetGeographicDistributionAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Tüm dashboard verilerini tek seferde getirir (özet)
        /// </summary>
        /// <returns>Tam dashboard verileri</returns>
        [HttpGet("full")]
        [ProducesResponseType(typeof(ResponseModel<FullDashboardDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<FullDashboardDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetFullDashboard()
        {
            try
            {
                var kpiTask = _dashboardService.GetKpiAsync();
                var financialTask = _dashboardService.GetFinancialDashboardAsync();
                var alertsTask = _dashboardService.GetCriticalAlertsAsync();
                var geoTask = _dashboardService.GetGeographicDistributionAsync();
                var productTask = _dashboardService.GetProductStatisticsAsync();

                await Task.WhenAll(kpiTask, financialTask, alertsTask, geoTask, productTask);

                var fullDashboard = new FullDashboardDto
                {
                    Kpi = kpiTask.Result.Data,
                    Financial = financialTask.Result.Data,
                    CriticalAlerts = alertsTask.Result.Data,
                    GeographicDistribution = geoTask.Result.Data,
                    ProductStatistics = productTask.Result.Data
                };

                return Ok(ResponseModel<FullDashboardDto>.Success(fullDashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFullDashboard");
                return StatusCode(500, ResponseModel<FullDashboardDto>.Fail(
                    $"Dashboard verileri getirilirken hata oluştu: {ex.Message}",
                    Core.Enums.StatusCode.Error));
            }
        }




        [HttpGet("ykb/kpi")]
        public async Task<IActionResult> GetYkbKpi([FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null)
        {
            var result = await _dashboardService.GetYkbKpiAsync(from, to);
            return Ok(result);
        }

        [HttpGet("technical-service-status-counts")]
        public async Task<IActionResult> GetTechnicalServiceStatusCounts()
        {
            var result = await _dashboardService.GetMyTechnicalServiceStatusCountsAsync();
            return Ok(result);
        }

        [HttpGet("ykb/technical-service-status-counts")]
        public async Task<IActionResult> YkbGetTechnicalServiceStatusCounts()
        {
            var result = await _dashboardService.YkbGetMyTechnicalServiceStatusCountsAsync();
            return Ok(result);
        }
       
        [HttpGet("qnb/technical-service-status-counts")]
        public async Task<IActionResult> QnbGetTechnicalServiceStatusCounts() 
        {
            var result = await _dashboardService.QnbGetMyTechnicalServiceStatusCountsAsync();
            return Ok(result);
        }

    }

    /// <summary>
    /// Tüm dashboard verilerini içeren DTO
    /// </summary>
    public class FullDashboardDto
    {
        public DashboardKpiDto? Kpi { get; set; }
        public FinancialDashboardDto? Financial { get; set; }
        public CriticalAlertsDto? CriticalAlerts { get; set; }
        public GeographicDistributionDto? GeographicDistribution { get; set; }
        public ProductStatisticsDto? ProductStatistics { get; set; }
    }
}