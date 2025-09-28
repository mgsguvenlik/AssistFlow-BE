// WebAPI/Controllers/CitiesController.cs
using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.City;
using Model.Dtos.Region;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CitiesController : ControllerBase
{
    private readonly ICityService _service;
    public CitiesController(ICityService service) => _service = service;

    // GET: api/cities/all
    [HttpGet("all")]
    [ProducesResponseType(typeof(ResponseModel<List<CityGetDto>>), 200)]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllWithRegionsAsync());

    // GET: api/cities/{id}
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ResponseModel<CityGetDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
        => StatusCode((int)(await _service.GetCityByIdAsync(id)).StatusCode,
                      await _service.GetCityByIdAsync(id));

    // GET: api/cities/{id}/regions
    [HttpGet("{id:long}/regions")]
    [ProducesResponseType(typeof(ResponseModel<List<RegionGetDto>>), 200)]
    public async Task<IActionResult> GetRegions(long id)
        => Ok(await _service.GetRegionsByCityIdAsync(id));
}
