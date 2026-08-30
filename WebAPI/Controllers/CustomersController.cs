using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Customer;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("CustomerList")]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : CrudControllerBase<CustomerCreateDto, CustomerUpdateDto, CustomerGetDto, long>
    {
        private readonly ICustomerService _customerService;
        private readonly IActivationRecordService _activationRecordService;
        public CustomersController(
        ICrudService<CustomerCreateDto, CustomerUpdateDto, CustomerGetDto, long> service,
        ICustomerService customerService,
        ILogger<CustomersController> logger,
        IActivationRecordService activationRecordService)
       : base(service, logger)
        {
            _customerService = customerService;
            _activationRecordService = activationRecordService;
        }


        /// <summary>
        /// Müşteri listesini bir json dosyasınadn import etmek için bu metot kullanılır
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        [HttpPost("import-from-file")]
        [MenuAuthorize("CustomerList", MenuPermission.Edit)]
        public async Task<IActionResult> ImportFromFile([FromQuery] string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest("filePath parametresi boş olamaz.");

            var result = await _customerService.ImportFromFileAsync(filePath);

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("get-customer-activity/{customerId}")]
        [MenuAuthorize("CustomerList", MenuPermission.View)]
        public async Task<IActionResult> GetUserActivityRecords([FromRoute] int customerId, [FromQuery] QueryParams q)
        {
            var result = await _activationRecordService.GetCustomerActivity(customerId, q);
            return ToActionResult(result);
        }

        /// <summary>
        /// Tenant koduna göre müşterileri getirir. TenantCode null ise tüm müşteriler döner.
        /// </summary>
        /// <param name="tenantCode">Tenant kodu (nullable)</param>
        /// <param name="q">Sayfalama ve arama parametreleri</param>
        /// <returns>Sayfalanmış müşteri listesi</returns>
        [HttpGet("by-tenant")]
        [MenuAuthorize(new[] { "CustomerList", "ServiceRequestCreate", "YkbServiceRequestCreate", "QnbServiceRequestCreate" }, MenuPermission.View)]
        public async Task<IActionResult> GetByTenantCode([FromQuery] string? tenantCode, [FromQuery] QueryParams q)
        {
            var result = await _customerService.GetByTenantCodeAsync(tenantCode, q);
            return ToActionResult(result);
        }
    }
}
