using Microsoft.AspNetCore.Mvc;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/geocode")]
public class GeocodingController : ControllerBase
{
    private readonly GeocodingService _geocoder;

    public GeocodingController(GeocodingService geocoder) => _geocoder = geocoder;

    [HttpGet]
    public async Task<ActionResult<List<GeocodingService.GeocodeResult>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("q (query) is required");
        var results = await _geocoder.SearchAsync(q, HttpContext.RequestAborted);
        return Ok(results);
    }
}
