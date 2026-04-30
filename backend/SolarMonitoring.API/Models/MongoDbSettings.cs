namespace SolarMonitoring.API.Models;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string TelemetryCollection { get; set; } = "telemetry";
    public string AlertsCollection { get; set; } = "alerts";
    public string PredictionsCollection { get; set; } = "predictions";
}
