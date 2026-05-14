using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// In-memory fallback so the API works without a live MongoDB connection.
/// Seeds a rolling 24h of synthetic readings + a few sample alerts.
public class InMemoryStore
{
    private readonly object _lock = new();
    private readonly List<TelemetryReading> _readings = new();
    private readonly List<Alert> _alerts = new();
    private readonly List<Problem> _problems = new();

    public InMemoryStore()
    {
        // Synthetic seed removed: controllers now read from MongoDB.
        // The store remains as a write-through mirror for IngestionService;
        // nothing reads it. Safe to delete once IngestionService is updated.
    }

    public IReadOnlyList<TelemetryReading> GetReadings(int limit = 200)
    {
        lock (_lock)
        {
            return _readings
                .OrderByDescending(r => r.Timestamp)
                .Take(limit)
                .ToList();
        }
    }

    public TelemetryReading? GetLatest()
    {
        lock (_lock)
        {
            return _readings.OrderByDescending(r => r.Timestamp).FirstOrDefault();
        }
    }

    public void AddReading(TelemetryReading reading)
    {
        lock (_lock)
        {
            reading.Id ??= Guid.NewGuid().ToString("N");
            _readings.Add(reading);
        }
    }

    public IReadOnlyList<Alert> GetAlerts(bool includeAcknowledged = false)
    {
        lock (_lock)
        {
            return _alerts
                .Where(a => includeAcknowledged || !a.Acknowledged)
                .OrderByDescending(a => a.Timestamp)
                .ToList();
        }
    }

    public void AddAlert(Alert alert)
    {
        lock (_lock)
        {
            alert.Id ??= Guid.NewGuid().ToString("N");
            _alerts.Add(alert);
        }
    }

    public bool Acknowledge(string id)
    {
        lock (_lock)
        {
            var a = _alerts.FirstOrDefault(x => x.Id == id);
            if (a is null) return false;
            a.Acknowledged = true;
            return true;
        }
    }

    public IReadOnlyList<Problem> GetProblems()
    {
        lock (_lock)
        {
            return _problems.OrderByDescending(p => p.StartedAt).ToList();
        }
    }

    private void Seed()
    {
        var now = DateTime.UtcNow;
        var rnd = new Random(42);

        for (int i = 24 * 12; i >= 0; i--)
        {
            var ts = now.AddMinutes(-i * 5);
            var hour = ts.Hour + ts.Minute / 60.0;
            var solar = Math.Max(0, Math.Sin((hour - 6) / 12.0 * Math.PI));
            var pvPower = solar * 1800 + rnd.NextDouble() * 80;
            var load = 200 + rnd.NextDouble() * 400;

            _readings.Add(new TelemetryReading
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = ts,
                Source = "mpp-solar",
                Data = new InverterData
                {
                    Command = "QPIGS",
                    CommandDescription = "General Status Parameters inquiry",
                    AcInputVoltage = 240 + rnd.NextDouble() * 2 - 1,
                    AcInputFrequency = 50.0,
                    AcOutputVoltage = 230 + rnd.NextDouble() * 2 - 1,
                    AcOutputFrequency = 49.9,
                    AcOutputApparentPower = load + 20,
                    AcOutputActivePower = load,
                    AcOutputLoad = (load / 3000.0) * 100,
                    BusVoltage = 430 + rnd.NextDouble() * 2,
                    BatteryVoltage = 26.5 + solar * 1.5,
                    BatteryChargingCurrent = solar * 30,
                    BatteryCapacity = Math.Min(100, 60 + solar * 40),
                    InverterHeatSinkTemperature = 35 + solar * 15,
                    PvInputCurrentForBattery = solar * 6,
                    PvInputVoltage = solar > 0.05 ? 220 + solar * 30 : 0,
                    BatteryVoltageFromScc = solar > 0.05 ? 27.0 + solar : 0,
                    BatteryDischargeCurrent = solar < 0.1 ? 4 : 0,
                    IsLoadOn = 1,
                    IsChargingOn = solar > 0.1 ? 1 : 0,
                    IsSccChargingOn = solar > 0.1 ? 1 : 0,
                    IsAcChargingOn = 0,
                    PvInputPower = pvPower,
                    IsSwitchedOn = 1
                }
            });
        }

        _alerts.Add(new Alert
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = now.AddMinutes(-37),
            Severity = AlertSeverity.Warning,
            Code = "TEMP_HIGH",
            Title = "Inverter heat sink temperature elevated",
            Message = "Heat sink reached 52°C. Verify ventilation and clear obstructions."
        });
        _alerts.Add(new Alert
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = now.AddHours(-3),
            Severity = AlertSeverity.Info,
            Code = "PV_LOW_OUTPUT",
            Title = "PV output below expected",
            Message = "Midday output 18% below 7-day rolling average. Check for shading or soiling."
        });
        _alerts.Add(new Alert
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = now.AddHours(-9),
            Severity = AlertSeverity.Critical,
            Code = "BATTERY_LOW",
            Title = "Battery capacity dropped below 20%",
            Message = "SOC reached 18% overnight. Consider load shedding rules or grid charge fallback.",
            Acknowledged = true
        });

        _problems.Add(new Problem
        {
            Id = "prob-1", Code = "TEMP_HIGH", Severity = AlertSeverity.Warning,
            Title = "Inverter heat sink temperature elevated",
            Message = "Heat sink temperature exceeded 50°C threshold for 42 minutes.",
            StartedAt = now.AddMinutes(-72), EndedAt = null, Occurrences = 1
        });
        _problems.Add(new Problem
        {
            Id = "prob-2", Code = "PV_LOW_OUTPUT", Severity = AlertSeverity.Warning,
            Title = "PV underperforming for time of day",
            Message = "PV power 18% below rolling average between 12:10–14:55.",
            StartedAt = now.AddHours(-4), EndedAt = now.AddHours(-1), Occurrences = 1
        });
        _problems.Add(new Problem
        {
            Id = "prob-3", Code = "BATTERY_LOW", Severity = AlertSeverity.Critical,
            Title = "Battery capacity dropped below 20%",
            Message = "State of charge reached 18% overnight before sunrise recovery.",
            StartedAt = now.AddHours(-11), EndedAt = now.AddHours(-7), Occurrences = 1
        });
        _problems.Add(new Problem
        {
            Id = "prob-4", Code = "GRID_UNDERVOLT", Severity = AlertSeverity.Warning,
            Title = "Grid undervoltage events",
            Message = "AC input dipped below 200 V briefly on three separate occasions.",
            StartedAt = now.AddHours(-26), EndedAt = now.AddHours(-25.5), Occurrences = 3
        });
        _problems.Add(new Problem
        {
            Id = "prob-5", Code = "COMM_LOSS", Severity = AlertSeverity.Info,
            Title = "Telemetry gap",
            Message = "No data received from Raspberry Pi for 6 minutes.",
            StartedAt = now.AddHours(-30), EndedAt = now.AddHours(-30).AddMinutes(6), Occurrences = 1
        });
    }

}
