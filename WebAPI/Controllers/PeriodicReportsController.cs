using Business.Interfaces;
using Business.Interfaces.PeriodicReports;
using Core.Common;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.PeriodicReports;

namespace WebAPI.Controllers
{
    [Authorize(Roles = "ADMIN,Admin")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public sealed class PeriodicReportsController : ControllerBase
    {
        private readonly IPeriodicReportService _reportService;
        private readonly IReportExecutionService _executionService;
        private readonly ICurrentUser _currentUser;

        public PeriodicReportsController(
            IPeriodicReportService reportService,
            IReportExecutionService executionService,
            ICurrentUser currentUser)
        {
            _reportService = reportService;
            _executionService = executionService;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> GetPaged(
            [FromQuery] QueryParams query,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.GetPagedAsync(query, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.GetByIdAsync(id, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] PeriodicReportUpsertDto dto,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.CreateAsync(dto, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("update/{id:long}")]
        public async Task<IActionResult> Update(
            [FromRoute] long id,
            [FromBody] PeriodicReportUpsertDto dto,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.UpdateAsync(id, dto, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("delete/{id:long}")]
        public async Task<IActionResult> Delete(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.DeleteAsync(id, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("{id:long}/run")]
        public async Task<IActionResult> RunNow(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            var me = await _currentUser.GetAsync(cancellationToken);
            if (me == null || me.Id <= 0)
                return Unauthorized(ResponseModel.Fail("Kullanıcı bilgisi bulunamadı.", Core.Enums.StatusCode.Unauthorized));

            var outcome = await _executionService.ExecuteAsync(
                id,
                PeriodicReportTriggerType.Manual,
                me.Id,
                cancellationToken);

            var dto = new PeriodicReportRunResultDto
            {
                ExecutionId = outcome.ExecutionId,
                Status = outcome.Status,
                Message = outcome.Message
            };

            if (!outcome.Acquired)
                return Conflict(ResponseModel<PeriodicReportRunResultDto>.Fail(outcome.Message ?? "Rapor çalıştırılamadı.", Core.Enums.StatusCode.Conflict, dto));

            return outcome.Status == PeriodicReportExecutionStatus.Success
                ? Ok(ResponseModel<PeriodicReportRunResultDto>.Success(dto, outcome.Message))
                : StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ResponseModel<PeriodicReportRunResultDto>.Fail(outcome.Message ?? "Rapor çalıştırılamadı.", Core.Enums.StatusCode.Error, dto));
        }

        [HttpPost("preview")]
        public async Task<IActionResult> Preview(
            [FromBody] PeriodicReportPreviewRequestDto dto,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.PreviewAsync(dto, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("{id:long}/executions")]
        public async Task<IActionResult> GetExecutions(
            [FromRoute] long id,
            [FromQuery] QueryParams query,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.GetExecutionsAsync(id, query, cancellationToken);
            return ToActionResult(result);
        }

        private IActionResult ToActionResult<T>(ResponseModel<T> response) =>
            StatusCode((int)response.StatusCode, response);
    }
}
