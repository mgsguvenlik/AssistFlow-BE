using Business.Interfaces.Qnb;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.OvertimeReport;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class QnbOvertimeReportController : ControllerBase
    {
        private readonly IQnbOvertimeReportService _qnbOvertimeReportService;
        private readonly ILogger<QnbOvertimeReportController> _logger;

        public QnbOvertimeReportController(
            IQnbOvertimeReportService qnbOvertimeReportService,
            ILogger<QnbOvertimeReportController> logger)
        {
            _qnbOvertimeReportService = qnbOvertimeReportService;
            _logger = logger;
        }

        /// <summary>
        /// Belirli bir teknisyenin QNB fazla mesai raporunu getirir
        /// </summary>
        [HttpGet("technician/{technicianId}")]
        [ProducesResponseType(typeof(ResponseModel<QnbTechnicianOvertimeReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<QnbTechnicianOvertimeReportDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ResponseModel<QnbTechnicianOvertimeReportDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTechnicianOvertimeReport(
            [FromRoute] long technicianId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] bool includeCustomerDetails = true)
        {
            var result = await _qnbOvertimeReportService.GetTechnicianOvertimeReportAsync(
                technicianId,
                startDate,
                endDate,
                includeCustomerDetails);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Tüm teknisyenlerin QNB fazla mesai özetini getirir
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ResponseModel<QnbAllTechniciansOvertimeSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<QnbAllTechniciansOvertimeSummaryDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllTechniciansOvertimeSummary(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var result = await _qnbOvertimeReportService.GetAllTechniciansOvertimeSummaryAsync(
                startDate,
                endDate);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// QNB fazla mesai raporunu Excel formatýnda export eder
        /// </summary>
        [HttpGet("export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<byte[]>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportOvertimeReport(
            [FromQuery] long? technicianId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var result = await _qnbOvertimeReportService.ExportOvertimeReportToExcelAsync(
                technicianId,
                startDate,
                endDate);

            if (!result.IsSuccess)
                return StatusCode((int)result.StatusCode, result);

            var fileName = technicianId.HasValue
                ? $"QNB_FazlaMesai_Teknisyen_{technicianId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx"
                : $"QNB_FazlaMesai_TumTeknisyenler_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";

            return File(
                result.Data!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}