using Business.Interfaces;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Authorization;
using Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("WorkFlowSlaSettingList")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WorkFlowSlaSettingController
        : CrudControllerBase<WorkFlowSlaSettingCreateDto, WorkFlowSlaSettingUpdateDto, WorkFlowSlaSettingGetDto, long>
    {
        private readonly IWorkFlowSlaSettingService _slaService;

        public WorkFlowSlaSettingController(
            ICrudService<WorkFlowSlaSettingCreateDto, WorkFlowSlaSettingUpdateDto, WorkFlowSlaSettingGetDto, long> service,
            IWorkFlowSlaSettingService slaService,
            ILogger<WorkFlowSlaSettingController> logger)
            : base(service, logger)
        {
            _slaService = slaService;
        }

        /// <summary>
        /// Belirli bir müşteri tipi ve öncelik için aktif SLA ayarını getirir
        /// </summary>
        /// <param name="customerType">Müşteri tipi (0=General, 1=Ykb, 2=Individual, 3=Corporate)</param>
        /// <param name="priority">Öncelik (0=Low, 1=Normal, 2=High, 3=Urgent)</param>
        /// <response code="200">SLA ayarı bulundu</response>
        /// <response code="404">SLA ayarı bulunamadı</response>
        [HttpGet("by-type-priority")]
        [MenuAuthorize("WorkFlowSlaSettingList", MenuPermission.View)]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByTypeAndPriority(
            [FromQuery] WorkFlowCustomerType customerType,
            [FromQuery] WorkFlowPriority priority)
        {
            var result = await _slaService.GetSlaSettingAsync(customerType, priority);
            return Ok(result);
        }

        [HttpGet("by-customer-type")]
        [MenuAuthorize("WorkFlowSlaSettingList", MenuPermission.View)]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByCustomerType(
        [FromQuery] WorkFlowCustomerType customerType)
        {
            var result = await _slaService.GetByCustomerTypeAsync(customerType);
            return Ok(result);
        }
    }
}
