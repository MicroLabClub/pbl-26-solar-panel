namespace SolarMonitoring.API.Models;

public class HourlyWeather
{
    public DateTime Hour { get; set; }
    public double CloudCover { get; set; }
    public double ShortwaveRadiation { get; set; }
    public double TemperatureC { get; set; }
}

public class WeatherForecastResponse
{
    public string InstallationId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime FetchedAt { get; set; }
    public List<HourlyWeather> Hourly { get; set; } = new();
}
