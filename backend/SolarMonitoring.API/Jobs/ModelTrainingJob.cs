using MongoDB.Driver;
using Quartz;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Jobs;

/// Nightly job: for each installation, join historical telemetry against the
/// historical weather archive, compute the residual ratio (actual / physics),
/// train a FastTree regression, and persist if there's enough signal.
[DisallowConcurrentExecution]
public class ModelTrainingJob : IJob
{
    private const int MinTrainingSamples = 24;

    private readonly MongoContext _mongo;
    private readonly WeatherForecastService _weather;
    private readonly PhysicsModel _physics;
    private readonly MlCorrectionModel _correction;
    private readonly ILogger<ModelTrainingJob> _logger;

    public ModelTrainingJob(
        MongoContext mongo,
        WeatherForecastService weather,
        PhysicsModel physics,
        MlCorrectionModel correction,
        ILogger<ModelTrainingJob> logger)
    {
        _mongo = mongo;
        _weather = weather;
        _physics = physics;
        _correction = correction;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var installations = await _mongo.Installations.Find(_ => true).ToListAsync(context.CancellationToken);
        foreach (var installation in installations)
        {
            try
            {
                await TrainOneAsync(installation, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Training failed for installation {Id}", installation.Id);
            }
        }
    }

    public async Task<TrainingResult> TrainOneAsync(Installation installation, CancellationToken ct = default)
    {
        if (installation.Id is null)
            return new TrainingResult(false, 0, "installation has no id");

        var earliest = await _mongo.Telemetry
            .Find(r => r.InstallationId == installation.Id)
            .SortBy(r => r.Timestamp)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        if (earliest is null)
            return new TrainingResult(false, 0, "no telemetry for this installation");

        var startDate = DateOnly.FromDateTime(earliest.Timestamp.ToUniversalTime());
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var weatherHistory = await _weather.GetHistoricalAsync(installation, startDate, endDate, ct);
        var weatherByHour = weatherHistory.ToDictionary(w => w.Hour);

        var allReadings = await _mongo.Telemetry
            .Find(r => r.InstallationId == installation.Id)
            .ToListAsync(ct);

        var readingsByHour = allReadings
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .ToDictionary(g => g.Key, g => g.Average(r => r.Data.PvInputPower));

        var features = new List<MlCorrectionModel.CorrectionFeatures>();
        var labels = new List<float>();

        foreach (var (hour, actualPower) in readingsByHour)
        {
            if (!weatherByHour.TryGetValue(hour, out var weather)) continue;
            var physics = _physics.PredictPowerWatts(installation, hour, weather);
            if (physics < 20) continue; // skip night / dusk where ratio is ill-defined

            var ratio = (float)(actualPower / physics);
            if (float.IsNaN(ratio) || float.IsInfinity(ratio)) continue;

            features.Add(new MlCorrectionModel.CorrectionFeatures
            {
                HourOfDay = hour.Hour,
                DayOfYear = hour.DayOfYear,
                CloudCover = (float)weather.CloudCover,
                ShortwaveRadiation = (float)weather.ShortwaveRadiation,
                TemperatureC = (float)weather.TemperatureC,
            });
            labels.Add(Math.Clamp(ratio, 0f, 2f));
        }

        _logger.LogInformation(
            "Installation {Id}: {ReadingHours} telemetry hours, {Weather} weather hours, {Joined} joined training samples",
            installation.Id, readingsByHour.Count, weatherByHour.Count, features.Count);

        if (features.Count < MinTrainingSamples)
            return new TrainingResult(false, features.Count,
                $"not enough samples ({features.Count} < {MinTrainingSamples}). Wait for more data.");

        var mae = _correction.TrainAndPersist(installation.Id, features, labels);
        return new TrainingResult(true, features.Count, $"trained ok, MAE={mae:F3}");
    }

    public record TrainingResult(bool Trained, int Samples, string Message);
}
