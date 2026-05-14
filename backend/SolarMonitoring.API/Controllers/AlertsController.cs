using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly MongoContext _mongo;

    public AlertsController(MongoContext mongo) => _mongo = mongo;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Alert>>> List(
        [FromQuery] bool includeAcknowledged = false,
        [FromQuery] string? installationId = null)
    {
        var filters = new List<FilterDefinition<Alert>>();
        if (!includeAcknowledged)
            filters.Add(Builders<Alert>.Filter.Eq(a => a.Acknowledged, false));
        if (!string.IsNullOrEmpty(installationId))
            filters.Add(Builders<Alert>.Filter.Eq(a => a.InstallationId, installationId));

        var filter = filters.Count == 0 ? Builders<Alert>.Filter.Empty : Builders<Alert>.Filter.And(filters);

        var alerts = await _mongo.Alerts
            .Find(filter)
            .SortByDescending(a => a.Timestamp)
            .ToListAsync();
        return Ok(alerts);
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> Acknowledge(string id)
    {
        var update = Builders<Alert>.Update.Set(a => a.Acknowledged, true);
        var result = await _mongo.Alerts.UpdateOneAsync(
            Builders<Alert>.Filter.Eq(a => a.Id, id),
            update);
        return result.MatchedCount == 0 ? NotFound() : NoContent();
    }

    [HttpGet("problems")]
    public async Task<ActionResult<IEnumerable<Problem>>> Problems()
    {
        var problems = await _mongo.Problems
            .Find(_ => true)
            .SortByDescending(p => p.StartedAt)
            .ToListAsync();
        return Ok(problems);
    }
}
