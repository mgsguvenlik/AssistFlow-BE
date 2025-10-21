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


    [HttpGet("calculate-distance")]
    public IActionResult CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        try
        {
            double distanceKm = GetDistanceInKm(lat1, lon1, lat2, lon2);
            double distanceMeters = distanceKm * 1000;

            return Ok(new
            {
                DistanceKm = Math.Round(distanceKm, 2),
                DistanceMeters = Math.Round(distanceMeters, 2),
                Message = "Mesafe başarıyla hesaplandı."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    private static double GetDistanceInKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Dünya yarıçapı (km)
        double latRad1 = ToRadians(lat1);
        double lonRad1 = ToRadians(lon1);
        double latRad2 = ToRadians(lat2);
        double lonRad2 = ToRadians(lon2);

        double deltaLat = latRad2 - latRad1;
        double deltaLon = lonRad2 - lonRad1;

        double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                   Math.Cos(latRad1) * Math.Cos(latRad2) *
                   Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c; // km cinsinden döner
    }

    private static double ToRadians(double deg) => deg * (Math.PI / 180);
}
