namespace SolarMonitoring.API.Models;

public class Problem
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int Occurrences { get; set; } = 1;
}

public class HistoryPoint
{
    public DateTime Hour { get; set; }
    public double ProducedPowerWatts { get; set; }
    public double ProducedEnergyWh { get; set; }
    public double LoadWatts { get; set; }
}

public class SystemCheck
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = "ok"; // ok | warn | critical
    public string Detail { get; set; } = string.Empty;
}
