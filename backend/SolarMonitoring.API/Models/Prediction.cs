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
    public string Method { get; set; } = "rolling-average + clear-sky";
    public double TotalPredictedEnergyKwh { get; set; }
    public List<HourlyPrediction> Hourly { get; set; } = new();
}
