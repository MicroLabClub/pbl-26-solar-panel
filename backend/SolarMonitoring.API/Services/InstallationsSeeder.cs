using MongoDB.Driver;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// One-shot startup task: seed the default installation if the collection is empty,
/// then backfill any telemetry readings that don't yet have installation_id.
public class InstallationsSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InstallationsSeeder> _logger;

    public InstallationsSeeder(IServiceProvider services, ILogger<InstallationsSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();

        var existing = await mongo.Installations.CountDocumentsAsync(_ => true, cancellationToken: ct);
        Installation defaultInstallation;
        if (existing == 0)
        {
            defaultInstallation = new Installation
            {
                Name = "MicroLab Solar System",
                Latitude = 47.0105,
                Longitude = 28.8638,
                Timezone = "Europe/Chisinau",
                SystemCapacityWatts = 3000,
                PanelTiltDeg = 30,
                PanelAzimuthDeg = 180,
                MqttDeviceId = "pi-microlab-01",
                Notes = "Studentilor Street 9/11, Chișinău, Moldova. " +
                        "Coordinates approximate; weather forecast grid is ~11km. " +
                        "System capacity is a default — will be auto-calibrated by ML correction.",
            };
            await mongo.Installations.InsertOneAsync(defaultInstallation, cancellationToken: ct);
            _logger.LogInformation("Seeded default installation '{Name}' (id={Id})",
                defaultInstallation.Name, defaultInstallation.Id);
        }
        else
        {
            defaultInstallation = await mongo.Installations
                .Find(_ => true)
                .FirstAsync(ct);
        }

        // Backfill: telemetry rows with no installation_id get attached to the (single) default.
        var totalInstallations = await mongo.Installations.CountDocumentsAsync(_ => true, cancellationToken: ct);
        if (totalInstallations == 1)
        {
            var filter = Builders<TelemetryReading>.Filter.Or(
                Builders<TelemetryReading>.Filter.Eq(r => r.InstallationId, null),
                Builders<TelemetryReading>.Filter.Exists(r => r.InstallationId, false));
            var update = Builders<TelemetryReading>.Update.Set(r => r.InstallationId, defaultInstallation.Id);
            var result = await mongo.Telemetry.UpdateManyAsync(filter, update, cancellationToken: ct);
            if (result.ModifiedCount > 0)
                _logger.LogInformation("Backfilled installation_id={Id} on {Count} telemetry readings",
                    defaultInstallation.Id, result.ModifiedCount);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
