using Microsoft.AspNetCore.Mvc;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictionsController : ControllerBase
{
    private readonly InMemoryStore _store;
    private readonly EnergyPredictor _predictor;

    public PredictionsController(InMemoryStore store, EnergyPredictor predictor)
    {
        _store = store;
        _predictor = predictor;
    }

    /// 24h energy production forecast (kWh + per-hour curve).
    [HttpGet]
    public ActionResult<PredictionResponse> Get([FromQuery] int hours = 24)
    {
        var recent = _store.GetReadings(24 * 12);
        return Ok(_predictor.Predict(recent, Math.Clamp(hours, 1, 72)));
    }
}
