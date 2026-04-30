using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

public class AlertEvaluator
{
    private const double TempWarnC = 50;
    private const double TempCritC = 65;
    private const double BatteryLowPct = 20;
    private const double BatteryCritPct = 10;
    private const double LoadHighPct = 85;
    private const double GridUnderVolt = 200;
    private const double GridOverVolt = 260;

    public IEnumerable<Alert> Evaluate(TelemetryReading reading)
    {
        var d = reading.Data;

        if (d.InverterHeatSinkTemperature >= TempCritC)
            yield return Make(reading, AlertSeverity.Critical, "TEMP_CRITICAL",
                "Inverter overheating",
                $"Heat sink at {d.InverterHeatSinkTemperature:F0}°C — shutdown risk.");
        else if (d.InverterHeatSinkTemperature >= TempWarnC)
            yield return Make(reading, AlertSeverity.Warning, "TEMP_HIGH",
                "Inverter heat sink temperature elevated",
                $"Heat sink at {d.InverterHeatSinkTemperature:F0}°C. Verify ventilation.");

        if (d.BatteryCapacity <= BatteryCritPct)
            yield return Make(reading, AlertSeverity.Critical, "BATTERY_CRITICAL",
                "Battery capacity critically low",
                $"SOC at {d.BatteryCapacity:F0}%. Immediate action required.");
        else if (d.BatteryCapacity <= BatteryLowPct)
            yield return Make(reading, AlertSeverity.Warning, "BATTERY_LOW",
                "Battery capacity low",
                $"SOC at {d.BatteryCapacity:F0}%. Consider load shedding.");

        if (d.AcOutputLoad >= LoadHighPct)
            yield return Make(reading, AlertSeverity.Warning, "LOAD_HIGH",
                "Output load near rated capacity",
                $"Load at {d.AcOutputLoad:F0}%. Approaching inverter limit.");

        if (d.AcInputVoltage > 0 && d.AcInputVoltage < GridUnderVolt)
            yield return Make(reading, AlertSeverity.Warning, "GRID_UNDERVOLT",
                "Grid undervoltage",
                $"AC input {d.AcInputVoltage:F1} V. Below safe range.");

        if (d.AcInputVoltage > GridOverVolt)
            yield return Make(reading, AlertSeverity.Warning, "GRID_OVERVOLT",
                "Grid overvoltage",
                $"AC input {d.AcInputVoltage:F1} V. Above safe range.");

        var hour = reading.Timestamp.Hour;
        if (hour >= 10 && hour <= 15 && d.PvInputVoltage > 100 && d.PvInputPower < 50)
            yield return Make(reading, AlertSeverity.Warning, "PV_LOW_OUTPUT",
                "PV producing far below expected for time of day",
                "Check for shading, soiling, or panel disconnect.");
    }

    private static Alert Make(TelemetryReading r, AlertSeverity sev, string code, string title, string msg) =>
        new()
        {
            Timestamp = r.Timestamp,
            Severity = sev,
            Code = code,
            Title = title,
            Message = msg,
            SourceReadingId = r.Id
        };
}
