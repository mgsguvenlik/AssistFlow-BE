using Business.Interfaces;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.CustomerSystemAssignment;
using WebAPI.Controllers;

//[Authorize]
[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class CustomerSystemAssignmentsController : CrudControllerBase<
    CustomerSystemAssignmentCreateDto,
    CustomerSystemAssignmentUpdateDto,
    CustomerSystemAssignmentGetDto,
    long>
{
    private readonly ICustomerSystemAssignmentService _service;

    public CustomerSystemAssignmentsController(
        ICrudService<CustomerSystemAssignmentCreateDto, CustomerSystemAssignmentUpdateDto, CustomerSystemAssignmentGetDto, long> baseService,
        ICustomerSystemAssignmentService service,
        ILogger<CustomerSystemAssignmentsController> logger)
        : base(baseService, logger)
    {
        _service = service;
    }

    [HttpGet("by-customer/{customerId:long}")]
    public async Task<IActionResult> GetByCustomerId(long customerId)
    {
        if (customerId <= 0)
            return BadRequest("Geçersiz müşteri Id.");

        var result = await _service.GetByCustomerIdAsync(customerId);

        // Boş liste dönebilir, bu normal bir durum – 200 OK + [] yeterli
        return Ok(result);
    }

}
