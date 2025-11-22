using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Customer;

namespace WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : CrudControllerBase<CustomerCreateDto, CustomerUpdateDto, CustomerGetDto, long>
    {
        private readonly ICustomerService _customerService;
        public CustomersController(
        ICrudService<CustomerCreateDto, CustomerUpdateDto, CustomerGetDto, long> service,
        ICustomerService customerService,
        ILogger<CustomersController> logger)
       : base(service, logger)
        {
            _customerService = customerService;
        }


        /// <summary>
        /// Müşteri listesini bir json dosyasınadn import etmek için bu metot kullanılır
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        [HttpPost("import-from-file")]
        public async Task<IActionResult> ImportFromFile([FromQuery] string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest("filePath parametresi boş olamaz.");

            var result = await _customerService.ImportFromFileAsync(filePath);

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
