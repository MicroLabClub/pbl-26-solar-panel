namespace SolarMonitoring.API.Models;

/// Unified shape for any time-series chart on the dashboard.
/// `BatteryFlowWatts` is positive when charging, negative when discharging.
public class SeriesPoint
{
    public DateTime Timestamp { get; set; }
    public double PvPowerWatts { get; set; }
    public double LoadWatts { get; set; }
    public double BatterySoc { get; set; }
    public double BatteryVoltage { get; set; }
    public double BatteryFlowWatts { get; set; }
}
