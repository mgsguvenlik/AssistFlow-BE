
using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.OvertimeReport;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OvertimeReportController : ControllerBase
    {
        private readonly IOvertimeReportService _overtimeReportService;
        private readonly ILogger<OvertimeReportController> _logger;

        public OvertimeReportController(
            IOvertimeReportService overtimeReportService,
            ILogger<OvertimeReportController> logger)
        {
            _overtimeReportService = overtimeReportService;
            _logger = logger;
        }

        /// <summary>
        /// Belirli bir teknisyenin fazla mesai raporunu getirir
        /// </summary>
        /// <param name="technicianId">Teknisyen ID</param>
        /// <param name="startDate">Baþlangýç tarihi</param>
        /// <param name="endDate">Bitiþ tarihi</param>
        /// <param name="includeCustomerDetails">Müþteri detaylarýný dahil et</param>
        /// <returns>Teknisyen fazla mesai raporu</returns>
        [HttpGet("technician/{technicianId}")]
        [ProducesResponseType(typeof(ResponseModel<TechnicianOvertimeReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<TechnicianOvertimeReportDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ResponseModel<TechnicianOvertimeReportDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTechnicianOvertimeReport(
            [FromRoute] long technicianId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] bool includeCustomerDetails =true)
        {
            var result = await _overtimeReportService.GetTechnicianOvertimeReportAsync(
                technicianId,
                startDate,
                endDate,
                includeCustomerDetails);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Tüm teknisyenlerin fazla mesai özetini getirir
        /// </summary> 
        /// <param name="endDate">Bitiþ tarihi</param>
        /// <returns>Tüm teknisyenler fazla mesai özeti</returns>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ResponseModel<AllTechniciansOvertimeSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<AllTechniciansOvertimeSummaryDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllTechniciansOvertimeSummary(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var result = await _overtimeReportService.GetAllTechniciansOvertimeSummaryAsync(startDate, endDate);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Fazla mesai raporunu Excel formatýnda export eder
        /// </summary>
        /// <param name="technicianId">Teknisyen ID (opsiyonel, belirtilmezse tüm teknisyenler)</param>
        /// <param name="startDate">Baþlangýç tarihi</param>
        /// <param name="endDate">Bitiþ tarihi</param>
        /// <returns>Excel dosyasý</returns>
        [HttpGet("export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<byte[]>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportOvertimeReport(
            [FromQuery] long? technicianId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var result = await _overtimeReportService.ExportOvertimeReportToExcelAsync(
                technicianId,
                startDate,
                endDate);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            var fileName = technicianId.HasValue
                ? $"FazlaMesai_Teknisyen_{technicianId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx"
                : $"FazlaMesai_TumTeknisyenler_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";

            return File(
                result.Data!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}