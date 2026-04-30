using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// Naive 24h forecast: uses recent peak PV as a system capacity proxy
/// and a clear-sky sinusoid centered on solar noon (12:00 local).
/// Replace with weather-aware model later.
public class EnergyPredictor
{
    public PredictionResponse Predict(IReadOnlyList<TelemetryReading> recent, int hours = 24)
    {
        var peak = recent.Count == 0
            ? 1500
            : Math.Max(500, recent.Select(r => r.Data.PvInputPower).DefaultIfEmpty(0).Max() * 1.05);

        var start = DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour);
        var hourly = new List<HourlyPrediction>(hours);
        double totalWh = 0;

        for (int i = 0; i < hours; i++)
        {
            var t = start.AddHours(i);
            var localHour = t.Hour;
            var solar = Math.Max(0, Math.Sin((localHour - 6) / 12.0 * Math.PI));
            var power = solar * peak;
            var energyWh = power;
            totalWh += energyWh;

            hourly.Add(new HourlyPrediction
            {
                Hour = t,
                PredictedPowerWatts = Math.Round(power, 1),
                PredictedEnergyWh = Math.Round(energyWh, 1),
                Confidence = solar > 0 ? 0.75 : 0.95
            });
        }

        return new PredictionResponse
        {
            TotalPredictedEnergyKwh = Math.Round(totalWh / 1000.0, 2),
            Hourly = hourly
        };
    }
}
