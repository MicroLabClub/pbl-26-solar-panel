using Microsoft.AspNetCore.Mvc;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly InMemoryStore _store;
    private readonly AlertEvaluator _evaluator;

    public TelemetryController(InMemoryStore store, AlertEvaluator evaluator)
    {
        _store = store;
        _evaluator = evaluator;
    }

    /// Latest reading from the inverter.
    [HttpGet("latest")]
    public ActionResult<TelemetryReading> Latest()
    {
        var latest = _store.GetLatest();
        return latest is null ? NotFound() : Ok(latest);
    }

    /// Recent readings, newest first.
    [HttpGet]
    public ActionResult<IEnumerable<TelemetryReading>> List([FromQuery] int limit = 200)
    {
        return Ok(_store.GetReadings(Math.Clamp(limit, 1, 2000)));
    }

    /// Ingest endpoint for the Raspberry Pi. Same JSON shape as the device payload.
    [HttpPost]
    public ActionResult<TelemetryReading> Ingest([FromBody] TelemetryReading reading)
    {
        if (reading is null) return BadRequest("payload required");
        if (reading.Timestamp == default) reading.Timestamp = DateTime.UtcNow;

        _store.AddReading(reading);

        foreach (var alert in _evaluator.Evaluate(reading))
            _store.AddAlert(alert);

        return CreatedAtAction(nameof(Latest), new { }, reading);
    }

    /// Hourly historical aggregates over the last `days` days.
    [HttpGet("history")]
    public ActionResult<IEnumerable<HistoryPoint>> History([FromQuery] int days = 2)
    {
        days = Math.Clamp(days, 1, 14);
        var since = DateTime.UtcNow.AddDays(-days);
        var readings = _store.GetReadings(2000)
            .Where(r => r.Timestamp >= since)
            .OrderBy(r => r.Timestamp)
            .ToList();

        var points = readings
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .Select(g => new HistoryPoint
            {
                Hour = g.Key,
                ProducedPowerWatts = Math.Round(g.Average(r => r.Data.PvInputPower), 1),
                ProducedEnergyWh = Math.Round(g.Average(r => r.Data.PvInputPower), 1),
                LoadWatts = Math.Round(g.Average(r => r.Data.AcOutputActivePower), 1),
            })
            .OrderBy(p => p.Hour)
            .ToList();

        return Ok(points);
    }

    /// Per-subsystem health check derived from the latest reading.
    [HttpGet("checks")]
    public ActionResult<IEnumerable<SystemCheck>> Checks()
    {
        var latest = _store.GetLatest();
        if (latest is null) return Ok(Array.Empty<SystemCheck>());
        var d = latest.Data;

        SystemCheck Make(string key, string label, string status, string detail) =>
            new() { Key = key, Label = label, Status = status, Detail = detail };

        var checks = new List<SystemCheck>
        {
            Make("pv", "PV Array",
                d.PvInputPower > 30 || d.PvInputVoltage < 50 ? "ok" : "warn",
                d.PvInputPower > 30
                    ? $"{Math.Round(d.PvInputPower)} W from {d.PvInputVoltage:F0} V input"
                    : "No production — expected if night / heavy cloud"),
            Make("inverter", "Inverter",
                d.IsSwitchedOn == 1 && d.InverterHeatSinkTemperature < 60 ? "ok"
                    : d.InverterHeatSinkTemperature >= 65 ? "critical" : "warn",
                $"Switched {(d.IsSwitchedOn == 1 ? "ON" : "OFF")} · {Math.Round(d.InverterHeatSinkTemperature)}°C heat sink"),
            Make("battery", "Battery Bank",
                d.BatteryCapacity >= 40 ? "ok" : d.BatteryCapacity >= 20 ? "warn" : "critical",
                $"{Math.Round(d.BatteryCapacity)}% SOC · {d.BatteryVoltage:F2} V"),
            Make("charging", "Solar Charge Controller",
                d.IsSccChargingOn == 1 || d.PvInputPower < 30 ? "ok" : "warn",
                d.IsSccChargingOn == 1
                    ? $"Charging at {d.BatteryChargingCurrent:F1} A"
                    : "Idle (no PV input)"),
            Make("load", "Output Load",
                d.AcOutputLoad < 80 ? "ok" : d.AcOutputLoad < 95 ? "warn" : "critical",
                $"{Math.Round(d.AcOutputActivePower)} W ({d.AcOutputLoad:F0}% of capacity)"),
            Make("grid", "Grid Connection",
                d.AcInputVoltage > 210 && d.AcInputVoltage < 250 ? "ok" : "warn",
                $"{d.AcInputVoltage:F1} V @ {d.AcInputFrequency:F1} Hz"),
            Make("comm", "Telemetry Link", "ok",
                $"Last sample at {latest.Timestamp:HH:mm:ss} UTC"),
        };
        return Ok(checks);
    }

    /// Aggregated production summary for dashboard tiles.
    [HttpGet("summary")]
    public ActionResult<object> Summary()
    {
        var readings = _store.GetReadings(24 * 12);
        if (readings.Count == 0) return Ok(new { });

        var latest = readings.First();
        var todayWh = readings
            .Where(r => r.Timestamp.Date == DateTime.UtcNow.Date)
            .Sum(r => r.Data.PvInputPower) * (5.0 / 60.0);

        return Ok(new
        {
            latest.Timestamp,
            currentPvPower = latest.Data.PvInputPower,
            currentLoad = latest.Data.AcOutputActivePower,
            batterySoc = latest.Data.BatteryCapacity,
            batteryVoltage = latest.Data.BatteryVoltage,
            heatSinkC = latest.Data.InverterHeatSinkTemperature,
            gridVoltage = latest.Data.AcInputVoltage,
            isCharging = latest.Data.IsChargingOn == 1,
            isLoadOn = latest.Data.IsLoadOn == 1,
            todayEnergyKwh = Math.Round(todayWh / 1000.0, 2),
            sampleCount = readings.Count
        });
    }
}
