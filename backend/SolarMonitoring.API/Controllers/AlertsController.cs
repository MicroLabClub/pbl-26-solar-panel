using Microsoft.AspNetCore.Mvc;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly InMemoryStore _store;

    public AlertsController(InMemoryStore store) => _store = store;

    [HttpGet]
    public ActionResult<IEnumerable<Alert>> List([FromQuery] bool includeAcknowledged = false)
        => Ok(_store.GetAlerts(includeAcknowledged));

    [HttpPost("{id}/acknowledge")]
    public IActionResult Acknowledge(string id)
        => _store.Acknowledge(id) ? NoContent() : NotFound();

    /// Problems with start / end intervals — drives the alerts page bottom panel.
    [HttpGet("problems")]
    public ActionResult<IEnumerable<Problem>> Problems() => Ok(_store.GetProblems());
}
