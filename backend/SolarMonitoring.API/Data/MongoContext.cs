using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Data;

public class MongoContext
{
    private readonly MongoDbSettings _settings;
    private readonly IMongoDatabase _database;

    public MongoContext(IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
    }

    public IMongoCollection<TelemetryReading> Telemetry =>
        _database.GetCollection<TelemetryReading>(_settings.TelemetryCollection);

    public IMongoCollection<Alert> Alerts =>
        _database.GetCollection<Alert>(_settings.AlertsCollection);

    public IMongoCollection<Problem> Problems =>
        _database.GetCollection<Problem>(_settings.ProblemsCollection);

    public IMongoCollection<Installation> Installations =>
        _database.GetCollection<Installation>(_settings.InstallationsCollection);
}
