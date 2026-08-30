using Business.Interfaces.Ykb;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.OvertimeReport;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuAuthorize("YkbOvertimeReport", MenuPermission.View)]
    [ApiController]
    [Route("api/[controller]")]
    public class YkbOvertimeReportController : ControllerBase
    {
        private readonly IYkbOvertimeReportService _ykbOvertimeReportService;
        private readonly ILogger<YkbOvertimeReportController> _logger;

        public YkbOvertimeReportController(
            IYkbOvertimeReportService ykbOvertimeReportService,
            ILogger<YkbOvertimeReportController> logger)
        {
            _ykbOvertimeReportService = ykbOvertimeReportService;
            _logger = logger;
        }

        /// <summary>
        /// Belirli bir teknisyenin YKB fazla mesai raporunu getirir
        /// </summary>
        /// <param name="technicianId">Teknisyen ID</param>
        /// <param name="startDate">Başlangıç tarihi</param>
        /// <param name="endDate">Bitiş tarihi</param>
        /// <param name="includeCustomerDetails">Müşteri detaylarını dahil et</param>
        /// <returns>Teknisyen fazla mesai raporu</returns>
        [HttpGet("technician/{technicianId}")]
        [ProducesResponseType(typeof(ResponseModel<YkbTechnicianOvertimeReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<YkbTechnicianOvertimeReportDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ResponseModel<YkbTechnicianOvertimeReportDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTechnicianOvertimeReport(
            [FromRoute] long technicianId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] bool includeCustomerDetails = true)
        {
            var result = await _ykbOvertimeReportService.GetTechnicianOvertimeReportAsync(
                technicianId,
                startDate,
                endDate,
                includeCustomerDetails);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Tüm teknisyenlerin YKB fazla mesai özetini getirir
        /// </summary>
        /// <param name="startDate">Başlangıç tarihi</param>
        /// <param name="endDate">Bitiş tarihi</param>
        /// <returns>Tüm teknisyenler fazla mesai özeti</returns>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ResponseModel<YkbAllTechniciansOvertimeSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<YkbAllTechniciansOvertimeSummaryDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllTechniciansOvertimeSummary(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var result = await _ykbOvertimeReportService.GetAllTechniciansOvertimeSummaryAsync(
                startDate,
                endDate);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// YKB fazla mesai raporunu Excel formatında export eder
        /// </summary>
        /// <param name="technicianId">Teknisyen ID (opsiyonel, belirtilmezse tüm teknisyenler)</param>
        /// <param name="startDate">Başlangıç tarihi</param>
        /// <param name="endDate">Bitiş tarihi</param>
        /// <returns>Excel dosyası</returns>
        [HttpGet("export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<byte[]>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportOvertimeReport(
            [FromQuery] long? technicianId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var result = await _ykbOvertimeReportService.ExportOvertimeReportToExcelAsync(
                technicianId,
                startDate,
                endDate);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            var fileName = technicianId.HasValue
                ? $"YKB_FazlaMesai_Teknisyen_{technicianId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx"
                : $"YKB_FazlaMesai_TumTeknisyenler_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";

            return File(
                result.Data!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}
