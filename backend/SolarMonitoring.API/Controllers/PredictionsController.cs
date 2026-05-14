using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictionsController : ControllerBase
{
    private readonly MongoContext _mongo;
    private readonly EnergyPredictor _predictor;

    public PredictionsController(MongoContext mongo, EnergyPredictor predictor)
    {
        _mongo = mongo;
        _predictor = predictor;
    }

    [HttpGet]
    public async Task<ActionResult<PredictionResponse>> Get([FromQuery] int hours = 24)
    {
        var recent = await _mongo.Telemetry
            .Find(_ => true)
            .SortByDescending(r => r.Timestamp)
            .Limit(24 * 12)
            .ToListAsync();

        return Ok(_predictor.Predict(recent, Math.Clamp(hours, 1, 72)));
    }
}
