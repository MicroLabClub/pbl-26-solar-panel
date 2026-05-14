using System.Collections.Concurrent;
using Microsoft.ML;
using Microsoft.ML.Data;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// Per-installation FastTree regression on the residual ratio (actual / physics).
/// Loaded from disk on first use; replaced by ModelTrainingJob.
/// Returns 1.0 (no correction) when no model exists yet — so the hybrid output
/// gracefully degrades to pure physics until training has happened.
public class MlCorrectionModel
{
    private const string ModelsDir = "models";
    private const double MinCorrection = 0.0;
    private const double MaxCorrection = 1.8;

    private readonly MLContext _ml = new(seed: 42);
    private readonly ConcurrentDictionary<string, LoadedModel> _models = new();
    private readonly ILogger<MlCorrectionModel> _logger;

    public MlCorrectionModel(ILogger<MlCorrectionModel> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(ModelsDir);
        foreach (var path in Directory.GetFiles(ModelsDir, "correction-*.zip"))
        {
            try
            {
                var installationId = Path.GetFileNameWithoutExtension(path).Replace("correction-", "");
                var loaded = _ml.Model.Load(path, out _);
                var engine = _ml.Model.CreatePredictionEngine<CorrectionFeatures, CorrectionPrediction>(loaded);
                _models[installationId] = new LoadedModel(engine, File.GetLastWriteTimeUtc(path));
                _logger.LogInformation("Loaded correction model for installation {Id} from {Path}", installationId, path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load correction model at {Path}", path);
            }
        }
    }

    public bool IsTrained => !_models.IsEmpty;

    public double PredictCorrection(Installation installation, DateTime utcHour, HourlyWeather weather)
    {
        if (installation.Id is null || !_models.TryGetValue(installation.Id, out var loaded))
            return 1.0;

        var features = new CorrectionFeatures
        {
            HourOfDay = utcHour.Hour,
            DayOfYear = utcHour.DayOfYear,
            CloudCover = (float)weather.CloudCover,
            ShortwaveRadiation = (float)weather.ShortwaveRadiation,
            TemperatureC = (float)weather.TemperatureC,
        };
        var prediction = loaded.Engine.Predict(features);
        return Math.Clamp(prediction.CorrectionRatio, MinCorrection, MaxCorrection);
    }

    /// Train a fresh model from (features, ratio) pairs and persist it.
    /// Returns the mean absolute error in correction-ratio units (~0.1 is good, >0.5 is poor).
    public double TrainAndPersist(string installationId, List<CorrectionFeatures> features, List<float> ratios)
    {
        if (features.Count < 20)
            throw new InvalidOperationException(
                $"Refusing to train with {features.Count} samples; need at least 20.");

        var data = features.Select((f, i) =>
        {
            f.CorrectionRatio = ratios[i];
            return f;
        }).ToList();

        var split = Math.Max(1, (int)(data.Count * 0.8));
        var trainData = _ml.Data.LoadFromEnumerable(data.Take(split));
        var testData = _ml.Data.LoadFromEnumerable(data.Skip(split));

        // Hyperparams tuned for small training sets (avoids overfitting on
        // first-week training data). FastTree grows shallow when there's little
        // signal; gets more aggressive automatically as data accumulates.
        var pipeline = _ml.Transforms.Concatenate(
                "Features",
                nameof(CorrectionFeatures.HourOfDay),
                nameof(CorrectionFeatures.DayOfYear),
                nameof(CorrectionFeatures.CloudCover),
                nameof(CorrectionFeatures.ShortwaveRadiation),
                nameof(CorrectionFeatures.TemperatureC))
            .Append(_ml.Regression.Trainers.FastTree(
                labelColumnName: nameof(CorrectionFeatures.CorrectionRatio),
                featureColumnName: "Features",
                numberOfTrees: 30,
                numberOfLeaves: 6,
                minimumExampleCountPerLeaf: 3));

        var trained = pipeline.Fit(trainData);
        var predictions = trained.Transform(testData);
        var metrics = _ml.Regression.Evaluate(predictions, labelColumnName: nameof(CorrectionFeatures.CorrectionRatio));

        var path = Path.Combine(ModelsDir, $"correction-{installationId}.zip");
        _ml.Model.Save(trained, trainData.Schema, path);

        var engine = _ml.Model.CreatePredictionEngine<CorrectionFeatures, CorrectionPrediction>(trained);
        _models[installationId] = new LoadedModel(engine, DateTime.UtcNow);

        _logger.LogInformation(
            "Trained correction model for installation {Id}: MAE={Mae:F3}, R²={R2:F3}, samples={N}",
            installationId, metrics.MeanAbsoluteError, metrics.RSquared, data.Count);

        return metrics.MeanAbsoluteError;
    }

    private record LoadedModel(PredictionEngine<CorrectionFeatures, CorrectionPrediction> Engine, DateTime LoadedAt);

    public class CorrectionFeatures
    {
        public float HourOfDay { get; set; }
        public float DayOfYear { get; set; }
        public float CloudCover { get; set; }
        public float ShortwaveRadiation { get; set; }
        public float TemperatureC { get; set; }

        [ColumnName("Label")]
        public float CorrectionRatio { get; set; }
    }

    public class CorrectionPrediction
    {
        [ColumnName("Score")]
        public float CorrectionRatio { get; set; }
    }
}
