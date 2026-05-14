using MongoDB.Driver;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// Composes the deterministic physics baseline with the learned ML correction.
/// Until the ML model has enough data to beat physics, correction = 1.0
/// and the output is pure physics+weather.
public class HybridPredictor
{
    private readonly PhysicsModel _physics;
    private readonly WeatherForecastService _weather;
    private readonly MlCorrectionModel _correction;
    private readonly MongoContext _mongo;

    public HybridPredictor(
        PhysicsModel physics,
        WeatherForecastService weather,
        MlCorrectionModel correction,
        MongoContext mongo)
    {
        _physics = physics;
        _weather = weather;
        _correction = correction;
        _mongo = mongo;
    }

    public async Task<PredictionResponse> PredictNextHoursAsync(
        Installation installation, int hours, CancellationToken ct = default)
    {
        var forecast = await _weather.GetAsync(installation, ct);
        var hourlyForecast = forecast?.Hourly ?? new List<HourlyWeather>();

        var startHour = AlignToHour(DateTime.UtcNow);
        var hourly = new List<HourlyPrediction>(hours);
        double totalWh = 0;

        for (int i = 0; i < hours; i++)
        {
            var t = startHour.AddHours(i);
            var weather = hourlyForecast.FirstOrDefault(h => h.Hour == t);
            double physics = 0;
            double correction = 1.0;
            if (weather is not null)
            {
                physics = _physics.PredictPowerWatts(installation, t, weather);
                correction = _correction.PredictCorrection(installation, t, weather);
            }
            var predicted = Math.Max(0, physics * correction);
            totalWh += predicted;

            hourly.Add(new HourlyPrediction
            {
                Hour = t,
                PredictedPowerWatts = Math.Round(predicted, 1),
                PredictedEnergyWh = Math.Round(predicted, 1),
                Confidence = weather is null ? 0.4 : (_correction.IsTrained ? 0.85 : 0.7),
            });
        }

        return new PredictionResponse
        {
            GeneratedAt = DateTime.UtcNow,
            Method = _correction.IsTrained ? "physics + ML correction" : "physics (clear-sky + clouds + temp)",
            TotalPredictedEnergyKwh = Math.Round(totalWh / 1000.0, 2),
            Hourly = hourly,
        };
    }

    public async Task<TodayPredictionResponse> PredictTodayAsync(
        Installation installation, CancellationToken ct = default)
    {
        var todayUtcStart = DateTime.UtcNow.Date;
        var tomorrowUtcStart = todayUtcStart.AddDays(1);

        var forecast = await _weather.GetAsync(installation, ct);
        var hourlyForecast = forecast?.Hourly ?? new List<HourlyWeather>();

        var todayReadings = await _mongo.Telemetry
            .Find(r => r.InstallationId == installation.Id
                    && r.Timestamp >= todayUtcStart
                    && r.Timestamp < tomorrowUtcStart)
            .ToListAsync(ct);

        var actualByHour = todayReadings
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .ToDictionary(g => g.Key, g => g.Average(r => r.Data.PvInputPower));

        var points = new List<TodayPredictionPoint>(24);
        double errorSum = 0;
        int errorCount = 0;

        for (int h = 0; h < 24; h++)
        {
            var t = todayUtcStart.AddHours(h);
            var weather = hourlyForecast.FirstOrDefault(w => w.Hour == t);
            double physics = 0;
            double correction = 1.0;
            if (weather is not null)
            {
                physics = _physics.PredictPowerWatts(installation, t, weather);
                correction = _correction.PredictCorrection(installation, t, weather);
            }
            var predicted = Math.Max(0, physics * correction);
            double? actual = actualByHour.TryGetValue(t, out var a) ? Math.Round(a, 1) : null;

            if (actual.HasValue)
            {
                errorSum += Math.Abs(predicted - actual.Value);
                errorCount++;
            }

            points.Add(new TodayPredictionPoint
            {
                Hour = t,
                PredictedWatts = Math.Round(predicted, 1),
                ActualWatts = actual,
                CloudCover = weather?.CloudCover ?? 0,
            });
        }

        return new TodayPredictionResponse
        {
            InstallationId = installation.Id!,
            GeneratedAt = DateTime.UtcNow,
            Method = _correction.IsTrained ? "physics + ML correction" : "physics (clear-sky + clouds + temp)",
            MeanAbsoluteErrorWatts = errorCount == 0 ? 0 : Math.Round(errorSum / errorCount, 1),
            Hourly = points,
        };
    }

    private static DateTime AlignToHour(DateTime t) =>
        new(t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc);
}
