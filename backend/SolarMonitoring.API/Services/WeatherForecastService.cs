using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// Open-Meteo client (free, no API key). Caches the per-installation forecast
/// for 30 minutes; falls back to stale data if Open-Meteo is unreachable.
public class WeatherForecastService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private const string BaseUrl =
        "https://api.open-meteo.com/v1/forecast" +
        "?hourly=cloud_cover,shortwave_radiation,temperature_2m" +
        "&forecast_days=2&timezone=UTC";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WeatherForecastService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public WeatherForecastService(IHttpClientFactory httpFactory, ILogger<WeatherForecastService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// Historical hourly weather (Open-Meteo Archive). Used by the model training job
    /// to join past telemetry against the actual past weather. No caching — this is
    /// called once per nightly training run per installation.
    public async Task<List<HourlyWeather>> GetHistoricalAsync(
        Installation installation, DateOnly startUtc, DateOnly endUtc, CancellationToken ct = default)
    {
        var url = "https://archive-api.open-meteo.com/v1/archive" +
                  "?hourly=cloud_cover,shortwave_radiation,temperature_2m&timezone=UTC" +
                  $"&latitude={installation.Latitude.ToString("F4", CultureInfo.InvariantCulture)}" +
                  $"&longitude={installation.Longitude.ToString("F4", CultureInfo.InvariantCulture)}" +
                  $"&start_date={startUtc:yyyy-MM-dd}&end_date={endUtc:yyyy-MM-dd}";

        var http = _httpFactory.CreateClient("open-meteo-archive");
        http.Timeout = TimeSpan.FromSeconds(20);
        var raw = await http.GetStringAsync(url, ct);
        var parsed = JsonSerializer.Deserialize<OpenMeteoResponse>(raw)
            ?? throw new InvalidOperationException("Open-Meteo archive returned empty body");
        if (parsed.Hourly is null) return new List<HourlyWeather>();

        var hourly = new List<HourlyWeather>(parsed.Hourly.Time.Count);
        for (int i = 0; i < parsed.Hourly.Time.Count; i++)
        {
            hourly.Add(new HourlyWeather
            {
                Hour = DateTime.SpecifyKind(
                    DateTime.Parse(parsed.Hourly.Time[i], CultureInfo.InvariantCulture),
                    DateTimeKind.Utc),
                CloudCover = parsed.Hourly.CloudCover.ElementAtOrDefault(i),
                ShortwaveRadiation = parsed.Hourly.ShortwaveRadiation.ElementAtOrDefault(i) ?? 0,
                TemperatureC = parsed.Hourly.Temperature.ElementAtOrDefault(i),
            });
        }
        return hourly;
    }

    public async Task<WeatherForecastResponse?> GetAsync(Installation installation, CancellationToken ct = default)
    {
        if (installation.Id is null) return null;

        _cache.TryGetValue(installation.Id, out var cached);
        if (cached is not null && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
            return cached.Data;

        try
        {
            var fresh = await FetchAsync(installation, ct);
            _cache[installation.Id] = new CacheEntry(DateTime.UtcNow, fresh);
            return fresh;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open-Meteo fetch failed for installation {Id} — falling back to stale cache",
                installation.Id);
            return cached?.Data;
        }
    }

    private async Task<WeatherForecastResponse> FetchAsync(Installation installation, CancellationToken ct)
    {
        var url = $"{BaseUrl}&latitude={installation.Latitude.ToString("F4", CultureInfo.InvariantCulture)}" +
                  $"&longitude={installation.Longitude.ToString("F4", CultureInfo.InvariantCulture)}";

        var http = _httpFactory.CreateClient("open-meteo");
        http.Timeout = TimeSpan.FromSeconds(10);
        var raw = await http.GetStringAsync(url, ct);
        var parsed = JsonSerializer.Deserialize<OpenMeteoResponse>(raw)
            ?? throw new InvalidOperationException("Open-Meteo returned empty body");
        if (parsed.Hourly is null || parsed.Hourly.Time.Count == 0)
            throw new InvalidOperationException("Open-Meteo returned no hourly data");

        var hourly = new List<HourlyWeather>(parsed.Hourly.Time.Count);
        for (int i = 0; i < parsed.Hourly.Time.Count; i++)
        {
            hourly.Add(new HourlyWeather
            {
                Hour = DateTime.SpecifyKind(
                    DateTime.Parse(parsed.Hourly.Time[i], CultureInfo.InvariantCulture),
                    DateTimeKind.Utc),
                CloudCover = parsed.Hourly.CloudCover.ElementAtOrDefault(i),
                ShortwaveRadiation = parsed.Hourly.ShortwaveRadiation.ElementAtOrDefault(i) ?? 0,
                TemperatureC = parsed.Hourly.Temperature.ElementAtOrDefault(i),
            });
        }

        return new WeatherForecastResponse
        {
            InstallationId = installation.Id!,
            Latitude = installation.Latitude,
            Longitude = installation.Longitude,
            FetchedAt = DateTime.UtcNow,
            Hourly = hourly,
        };
    }

    private record CacheEntry(DateTime FetchedAt, WeatherForecastResponse Data);

    private class OpenMeteoResponse
    {
        [JsonPropertyName("hourly")]
        public OpenMeteoHourly? Hourly { get; set; }
    }

    private class OpenMeteoHourly
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = new();
        [JsonPropertyName("cloud_cover")]
        public List<double> CloudCover { get; set; } = new();
        [JsonPropertyName("shortwave_radiation")]
        public List<double?> ShortwaveRadiation { get; set; } = new();
        [JsonPropertyName("temperature_2m")]
        public List<double> Temperature { get; set; } = new();
    }
}
