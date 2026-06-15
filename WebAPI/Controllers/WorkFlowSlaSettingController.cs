using Business.Interfaces;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting;

namespace WebAPI.Controllers
{
    [Authorize]
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
        /// Belirli bir müþteri tipi ve öncelik için aktif SLA ayarýný getirir
        /// </summary>
        /// <param name="customerType">Müþteri tipi (0=General, 1=Ykb, 2=Individual, 3=Corporate)</param>
        /// <param name="priority">Öncelik (0=Low, 1=Normal, 2=High, 3=Urgent)</param>
        /// <response code="200">SLA ayarý bulundu</response>
        /// <response code="404">SLA ayarý bulunamadý</response>
        [HttpGet("by-type-priority")]
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
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByCustomerType(
        [FromQuery] WorkFlowCustomerType customerType)
        {
            var result = await _slaService.GetByCustomerTypeAsync(customerType);
            return Ok(result);
        }
    }
}