using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Jobs;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstallationsController : ControllerBase
{
    private readonly MongoContext _mongo;
    private readonly WeatherForecastService _weather;
    private readonly HybridPredictor _predictor;

    public InstallationsController(MongoContext mongo, WeatherForecastService weather, HybridPredictor predictor)
    {
        _mongo = mongo;
        _weather = weather;
        _predictor = predictor;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Installation>>> List()
    {
        var items = await _mongo.Installations
            .Find(_ => true)
            .SortBy(i => i.Name)
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Installation>> Get(string id)
    {
        var item = await _mongo.Installations
            .Find(i => i.Id == id)
            .FirstOrDefaultAsync();
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<Installation>> Create([FromBody] Installation installation)
    {
        if (installation is null) return BadRequest("payload required");
        if (string.IsNullOrWhiteSpace(installation.Name)) return BadRequest("name required");
        if (string.IsNullOrWhiteSpace(installation.MqttDeviceId)) return BadRequest("mqttDeviceId required");

        var existing = await _mongo.Installations
            .Find(i => i.MqttDeviceId == installation.MqttDeviceId)
            .FirstOrDefaultAsync();
        if (existing is not null) return Conflict($"mqttDeviceId '{installation.MqttDeviceId}' already in use");

        installation.Id = null;
        installation.CreatedAt = DateTime.UtcNow;
        await _mongo.Installations.InsertOneAsync(installation);
        return CreatedAtAction(nameof(Get), new { id = installation.Id }, installation);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Installation>> Update(string id, [FromBody] Installation update)
    {
        if (update is null) return BadRequest("payload required");

        update.Id = id;
        var existing = await _mongo.Installations.Find(i => i.Id == id).FirstOrDefaultAsync();
        if (existing is null) return NotFound();

        update.CreatedAt = existing.CreatedAt;
        var result = await _mongo.Installations.ReplaceOneAsync(i => i.Id == id, update);
        return result.MatchedCount == 0 ? NotFound() : Ok(update);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _mongo.Installations.DeleteOneAsync(i => i.Id == id);
        return result.DeletedCount == 0 ? NotFound() : NoContent();
    }

    [HttpGet("{id}/forecast")]
    public async Task<ActionResult<WeatherForecastResponse>> Forecast(string id, [FromQuery] int hours = 48)
    {
        var installation = await _mongo.Installations.Find(i => i.Id == id).FirstOrDefaultAsync();
        if (installation is null) return NotFound();

        var forecast = await _weather.GetAsync(installation, HttpContext.RequestAborted);
        if (forecast is null) return StatusCode(503, "weather service unavailable");

        hours = Math.Clamp(hours, 1, forecast.Hourly.Count);
        forecast.Hourly = forecast.Hourly.Take(hours).ToList();
        return Ok(forecast);
    }

    [HttpGet("{id}/predictions")]
    public async Task<ActionResult<PredictionResponse>> Predictions(string id, [FromQuery] int hours = 24)
    {
        var installation = await _mongo.Installations.Find(i => i.Id == id).FirstOrDefaultAsync();
        if (installation is null) return NotFound();

        var prediction = await _predictor.PredictNextHoursAsync(
            installation, Math.Clamp(hours, 1, 72), HttpContext.RequestAborted);
        return Ok(prediction);
    }

    [HttpGet("{id}/predictions/today")]
    public async Task<ActionResult<TodayPredictionResponse>> PredictionsToday(string id)
    {
        var installation = await _mongo.Installations.Find(i => i.Id == id).FirstOrDefaultAsync();
        if (installation is null) return NotFound();

        var today = await _predictor.PredictTodayAsync(installation, HttpContext.RequestAborted);
        return Ok(today);
    }

    /// On-demand training trigger (also runs nightly at 02:00 UTC via Quartz).
    [HttpPost("{id}/train")]
    public async Task<ActionResult<ModelTrainingJob.TrainingResult>> Train(
        string id, [FromServices] ModelTrainingJob job)
    {
        var installation = await _mongo.Installations.Find(i => i.Id == id).FirstOrDefaultAsync();
        if (installation is null) return NotFound();

        var result = await job.TrainOneAsync(installation, HttpContext.RequestAborted);
        return Ok(result);
    }
}
