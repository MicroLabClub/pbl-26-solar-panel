using Quartz;
using SolarMonitoring.API.Services;

namespace SolarMonitoring.API.Jobs;

/// Runs on a Quartz cron schedule (default: every minute on :00).
/// Drains everything the MQTT subscriber has buffered and persists it
/// in a single batch.
[DisallowConcurrentExecution]
public class TelemetryPersistJob : IJob
{
    private readonly MqttMessageQueue _queue;
    private readonly TelemetryIngestionService _ingestion;
    private readonly ILogger<TelemetryPersistJob> _logger;

    public TelemetryPersistJob(
        MqttMessageQueue queue,
        TelemetryIngestionService ingestion,
        ILogger<TelemetryPersistJob> logger)
    {
        _queue = queue;
        _ingestion = ingestion;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var batch = _queue.DrainAll();
        if (batch.Count == 0)
        {
            _logger.LogDebug("TelemetryPersistJob tick — queue empty");
            return;
        }

        var written = await _ingestion.IngestAsync(batch, context.CancellationToken);
        _logger.LogInformation("TelemetryPersistJob persisted {Count} readings", written);
    }
}
