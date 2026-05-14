using MongoDB.Driver;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

public class TelemetryIngestionService
{
    private readonly MongoContext _mongo;
    private readonly InMemoryStore _store;
    private readonly AlertEvaluator _evaluator;
    private readonly ILogger<TelemetryIngestionService> _logger;

    public TelemetryIngestionService(
        MongoContext mongo,
        InMemoryStore store,
        AlertEvaluator evaluator,
        ILogger<TelemetryIngestionService> logger)
    {
        _mongo = mongo;
        _store = store;
        _evaluator = evaluator;
        _logger = logger;
    }

    public async Task<int> IngestAsync(IReadOnlyCollection<TelemetryReading> batch, CancellationToken ct = default)
    {
        if (batch.Count == 0) return 0;

        var deviceIdToInstallationId = await BuildDeviceIdMapAsync(ct);

        foreach (var r in batch)
        {
            if (r.Timestamp == default) r.Timestamp = DateTime.UtcNow;
            r.Id = null;
            r.InstallationId = ResolveInstallationId(r, deviceIdToInstallationId);
        }

        var alerts = new List<Alert>();
        foreach (var r in batch)
            alerts.AddRange(_evaluator.Evaluate(r));

        try
        {
            await _mongo.Telemetry.InsertManyAsync(batch, cancellationToken: ct);
            if (alerts.Count > 0)
                await _mongo.Alerts.InsertManyAsync(alerts, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB write failed for {Count} readings — keeping in-memory copy only", batch.Count);
        }

        foreach (var r in batch) _store.AddReading(r);
        foreach (var a in alerts) _store.AddAlert(a);

        return batch.Count;
    }

    public Task<int> IngestAsync(TelemetryReading reading, CancellationToken ct = default)
        => IngestAsync(new[] { reading }, ct);

    private async Task<DeviceMap> BuildDeviceIdMapAsync(CancellationToken ct)
    {
        var installations = await _mongo.Installations.Find(_ => true).ToListAsync(ct);
        var lookup = installations
            .Where(i => !string.IsNullOrWhiteSpace(i.MqttDeviceId))
            .ToDictionary(i => i.MqttDeviceId, i => i.Id!);
        // Single-installation fallback: if there's exactly one, unmatched readings attach to it.
        var fallback = installations.Count == 1 ? installations[0].Id : null;
        return new DeviceMap(lookup, fallback);
    }

    private string? ResolveInstallationId(TelemetryReading r, DeviceMap map)
    {
        if (!string.IsNullOrWhiteSpace(r.DeviceId) && map.ByDeviceId.TryGetValue(r.DeviceId, out var id))
            return id;

        if (map.Fallback is not null)
            return map.Fallback;

        _logger.LogWarning("Ingested reading has no device_id and no fallback installation — stored unassigned");
        return null;
    }

    private record DeviceMap(Dictionary<string, string> ByDeviceId, string? Fallback);
}
