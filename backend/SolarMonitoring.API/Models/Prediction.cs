namespace SolarMonitoring.API.Models;

public class HourlyPrediction
{
    public DateTime Hour { get; set; }
    public double PredictedPowerWatts { get; set; }
    public double PredictedEnergyWh { get; set; }
    public double Confidence { get; set; }
}

public class PredictionResponse
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Method { get; set; } = "physics";
    public double TotalPredictedEnergyKwh { get; set; }
    public List<HourlyPrediction> Hourly { get; set; } = new();
}

public class TodayPredictionPoint
{
    public DateTime Hour { get; set; }
    public double PredictedWatts { get; set; }
    public double? ActualWatts { get; set; }
    public double CloudCover { get; set; }
}

public class TodayPredictionResponse
{
    public string InstallationId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Method { get; set; } = "physics";
    public double MeanAbsoluteErrorWatts { get; set; }
    public List<TodayPredictionPoint> Hourly { get; set; } = new();
}
