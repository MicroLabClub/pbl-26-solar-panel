using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly MongoContext _mongo;
    private readonly TelemetryIngestionService _ingestion;

    public TelemetryController(MongoContext mongo, TelemetryIngestionService ingestion)
    {
        _mongo = mongo;
        _ingestion = ingestion;
    }

    private static FilterDefinition<TelemetryReading> ByInstallation(string? installationId) =>
        string.IsNullOrEmpty(installationId)
            ? Builders<TelemetryReading>.Filter.Empty
            : Builders<TelemetryReading>.Filter.Eq(r => r.InstallationId, installationId);

    [HttpGet("latest")]
    public async Task<ActionResult<TelemetryReading>> Latest([FromQuery] string? installationId = null)
    {
        var latest = await _mongo.Telemetry
            .Find(ByInstallation(installationId))
            .SortByDescending(r => r.Timestamp)
            .Limit(1)
            .FirstOrDefaultAsync();
        return latest is null ? NotFound() : Ok(latest);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TelemetryReading>>> List(
        [FromQuery] int limit = 200,
        [FromQuery] string? installationId = null)
    {
        limit = Math.Clamp(limit, 1, 2000);
        var readings = await _mongo.Telemetry
            .Find(ByInstallation(installationId))
            .SortByDescending(r => r.Timestamp)
            .Limit(limit)
            .ToListAsync();
        return Ok(readings);
    }

    [HttpPost]
    public async Task<ActionResult<TelemetryReading>> Ingest([FromBody] TelemetryReading reading)
    {
        if (reading is null) return BadRequest("payload required");
        await _ingestion.IngestAsync(reading);
        return CreatedAtAction(nameof(Latest), new { }, reading);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<HistoryPoint>>> History(
        [FromQuery] int days = 2,
        [FromQuery] string? installationId = null)
    {
        days = Math.Clamp(days, 1, 14);
        var since = DateTime.UtcNow.AddDays(-days);

        var filter = Builders<TelemetryReading>.Filter.And(
            ByInstallation(installationId),
            Builders<TelemetryReading>.Filter.Gte(r => r.Timestamp, since));

        var readings = await _mongo.Telemetry
            .Find(filter)
            .SortBy(r => r.Timestamp)
            .ToListAsync();

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

    [HttpGet("checks")]
    public async Task<ActionResult<IEnumerable<SystemCheck>>> Checks([FromQuery] string? installationId = null)
    {
        var latest = await _mongo.Telemetry
            .Find(ByInstallation(installationId))
            .SortByDescending(r => r.Timestamp)
            .Limit(1)
            .FirstOrDefaultAsync();

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

    /// Unified time-series for any dashboard chart. `hours` covers the window;
    /// granularity is adaptive: raw readings for windows ≤12h, hourly aggregates
    /// otherwise (keeps payload small for week-long views).
    [HttpGet("series")]
    public async Task<ActionResult<IEnumerable<SeriesPoint>>> Series(
        [FromQuery] int hours = 12,
        [FromQuery] string? installationId = null)
    {
        hours = Math.Clamp(hours, 1, 24 * 14);
        var since = DateTime.UtcNow.AddHours(-hours);

        var filter = Builders<TelemetryReading>.Filter.And(
            ByInstallation(installationId),
            Builders<TelemetryReading>.Filter.Gte(r => r.Timestamp, since));

        var readings = await _mongo.Telemetry
            .Find(filter)
            .SortBy(r => r.Timestamp)
            .ToListAsync();

        if (hours <= 12)
        {
            return Ok(readings.Select(r => MakePoint(r.Timestamp, r.Data)));
        }

        // Hourly aggregate for windows wider than 12h — keeps payload small.
        var aggregated = readings
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .Select(g => new SeriesPoint
            {
                Timestamp = g.Key,
                PvPowerWatts = Math.Round(g.Average(r => r.Data.PvInputPower), 1),
                LoadWatts = Math.Round(g.Average(r => r.Data.AcOutputActivePower), 1),
                BatterySoc = Math.Round(g.Average(r => r.Data.BatteryCapacity), 1),
                BatteryVoltage = Math.Round(g.Average(r => r.Data.BatteryVoltage), 2),
                BatteryFlowWatts = Math.Round(
                    g.Average(r => (r.Data.BatteryChargingCurrent - r.Data.BatteryDischargeCurrent) * r.Data.BatteryVoltage), 1),
            })
            .OrderBy(p => p.Timestamp)
            .ToList();
        return Ok(aggregated);
    }

    private static SeriesPoint MakePoint(DateTime t, InverterData d) => new()
    {
        Timestamp = t,
        PvPowerWatts = Math.Round(d.PvInputPower, 1),
        LoadWatts = Math.Round(d.AcOutputActivePower, 1),
        BatterySoc = Math.Round(d.BatteryCapacity, 1),
        BatteryVoltage = Math.Round(d.BatteryVoltage, 2),
        BatteryFlowWatts = Math.Round((d.BatteryChargingCurrent - d.BatteryDischargeCurrent) * d.BatteryVoltage, 1),
    };

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary([FromQuery] string? installationId = null)
    {
        var latest = await _mongo.Telemetry
            .Find(ByInstallation(installationId))
            .SortByDescending(r => r.Timestamp)
            .Limit(1)
            .FirstOrDefaultAsync();

        if (latest is null) return Ok(new { });

        var startOfTodayUtc = DateTime.UtcNow.Date;
        var todayFilter = Builders<TelemetryReading>.Filter.And(
            ByInstallation(installationId),
            Builders<TelemetryReading>.Filter.Gte(r => r.Timestamp, startOfTodayUtc));

        var todayReadings = await _mongo.Telemetry
            .Find(todayFilter)
            .SortBy(r => r.Timestamp)
            .ToListAsync();

        double todayWh = 0;
        for (int i = 1; i < todayReadings.Count; i++)
        {
            var dtHours = (todayReadings[i].Timestamp - todayReadings[i - 1].Timestamp).TotalHours;
            var avgPower = (todayReadings[i].Data.PvInputPower + todayReadings[i - 1].Data.PvInputPower) / 2.0;
            todayWh += avgPower * dtHours;
        }

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
            sampleCount = todayReadings.Count
        });
    }
}
