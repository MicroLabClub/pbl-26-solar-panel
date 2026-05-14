namespace SolarMonitoring.API.Models;

public class MqttSettings
{
    public string Host { get; set; } = "broker.hivemq.com";
    public int Port { get; set; } = 8883;
    public bool UseTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string ClientId { get; set; } = "solar-monitoring-api";
    public string Topic { get; set; } = "solar/+/telemetry";
    public string PersistCron { get; set; } = "0 * * * * ?"; // every minute on :00
}
