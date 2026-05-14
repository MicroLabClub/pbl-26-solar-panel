using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarMonitoring.API.Services;

/// Street-level geocoder backed by Nominatim (OpenStreetMap). Results cached for
/// one hour per query; requests are throttled to one per second to respect
/// Nominatim's public-usage policy.
public class GeocodingService
{
    private const string Endpoint = "https://nominatim.openstreetmap.org/search";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GeocodingService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastCallUtc = DateTime.MinValue;

    public GeocodingService(IHttpClientFactory httpFactory, ILogger<GeocodingService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<List<GeocodeResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();
        var key = query.Trim().ToLowerInvariant();
        if (_cache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < CacheTtl)
            return hit.Results;

        await _gate.WaitAsync(ct);
        try
        {
            // Nominatim asks for ≤1 request/second.
            var elapsed = DateTime.UtcNow - _lastCallUtc;
            if (elapsed < TimeSpan.FromSeconds(1))
                await Task.Delay(TimeSpan.FromSeconds(1) - elapsed, ct);

            var http = _httpFactory.CreateClient("nominatim");
            http.Timeout = TimeSpan.FromSeconds(10);

            var url = $"{Endpoint}?format=json&limit=5&addressdetails=1&q={Uri.EscapeDataString(query)}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("SolarMonitoring", "1.0"));
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("(Solar-Panel-Monitoring-System)"));
            req.Headers.AcceptLanguage.ParseAdd("en");

            var resp = await http.SendAsync(req, ct);
            _lastCallUtc = DateTime.UtcNow;
            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<List<NominatimResult>>(raw) ?? new();

            var results = parsed
                .Where(n => double.TryParse(n.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                .Select(n => new GeocodeResult
                {
                    Name = n.DisplayName ?? "",
                    Latitude = double.Parse(n.Lat!, CultureInfo.InvariantCulture),
                    Longitude = double.Parse(n.Lon!, CultureInfo.InvariantCulture),
                    Country = n.Address?.Country ?? "",
                    Admin1 = n.Address?.State ?? n.Address?.County ?? "",
                    Locality = n.Address?.City ?? n.Address?.Town ?? n.Address?.Village ?? "",
                    Type = n.Type ?? "",
                })
                .ToList();

            _cache[key] = new CacheEntry(DateTime.UtcNow, results);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocode failed for '{Query}'", query);
            return new();
        }
        finally
        {
            _gate.Release();
        }
    }

    private record CacheEntry(DateTime At, List<GeocodeResult> Results);

    public class GeocodeResult
    {
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Country { get; set; } = "";
        public string Admin1 { get; set; } = "";
        public string Locality { get; set; } = "";
        public string Type { get; set; } = "";
    }

    private class NominatimResult
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("address")] public NominatimAddress? Address { get; set; }
    }

    private class NominatimAddress
    {
        [JsonPropertyName("country")] public string? Country { get; set; }
        [JsonPropertyName("state")] public string? State { get; set; }
        [JsonPropertyName("county")] public string? County { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("town")] public string? Town { get; set; }
        [JsonPropertyName("village")] public string? Village { get; set; }
    }
}
