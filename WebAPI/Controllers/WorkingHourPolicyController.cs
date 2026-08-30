using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkingHourPolicy;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuAuthorize("WorkingHourPolicyList", MenuPermission.View)]
    [ApiController]
    [Route("api/[controller]")]
    public class WorkingHourPolicyController : ControllerBase
    {
        private readonly IWorkingHourPolicyService _policyService;
        private readonly ILogger<WorkingHourPolicyController> _logger;

        public WorkingHourPolicyController(
            IWorkingHourPolicyService policyService,
            ILogger<WorkingHourPolicyController> logger)
        {
            _policyService = policyService;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif mesai politikalarını getirir
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<WorkingHourPolicyGetDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllPolicies()
        {
            var result = await _policyService.GetAllPoliciesAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Belirli bir tarihe uygulanan politikaları getirir
        /// </summary>
        [HttpPost("date/{date}")]
        [MenuAuthorize("WorkingHourPolicyList", MenuPermission.View)]
        [ProducesResponseType(typeof(ResponseModel<List<WorkingHourPolicyGetDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPoliciesForDate([FromRoute] DateOnly date)
        {
            var result = await _policyService.GetPoliciesForDateAsync(date);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Politika oluşturur
        /// </summary>
        [HttpPost("create")]
        [MenuAuthorize("WorkingHourPolicyList", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<WorkingHourPolicyGetDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CreatePolicy([FromBody] WorkingHourPolicyCreateDto dto)
        {
            var result = await _policyService.CreatePolicyAsync(dto);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Politika günceller
        /// </summary>
        [HttpPost("update")]
        [MenuAuthorize("WorkingHourPolicyList", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<WorkingHourPolicyGetDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdatePolicy([FromBody] WorkingHourPolicyUpdateDto dto)
        {
            var result = await _policyService.UpdatePolicyAsync(dto);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Politika siler
        /// </summary>
        [HttpPost("delete/{id}")]
        [MenuAuthorize("WorkingHourPolicyList", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeletePolicy([FromRoute] long id)
        {
            var result = await _policyService.DeletePolicyAsync(id);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Politika aktif/pasif yapar
        /// </summary>
        [HttpPost("{id}/toggle")]
        [MenuAuthorize("WorkingHourPolicyList", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> TogglePolicy([FromRoute] long id, [FromQuery] bool isActive)
        {
            var result = await _policyService.TogglePolicyAsync(id, isActive);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Nager.Date API'den resmi tatilleri senkronize eder
        /// </summary>
        [HttpPost("sync/{year}")]
        [MenuAuthorize("WorkingHourPolicyList", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<SyncPublicHolidaysDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SyncPublicHolidays([FromRoute] int year)
        {
            var result = await _policyService.SyncPublicHolidaysFromApiAsync(year);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Default politikaları oluşturur (Hafta içi, Cumartesi, Pazar)
        /// </summary>
        [HttpPost("create-defaults")]
        [MenuAuthorize("WorkingHourPolicyList", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateDefaultPolicies()
        {
            var result = await _policyService.CreateDefaultPoliciesAsync();
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
