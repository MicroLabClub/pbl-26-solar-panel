using System.Threading.Channels;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// Bounded thread-safe queue between the MQTT subscriber (producer)
/// and the Quartz persist job (consumer).
public class MqttMessageQueue
{
    private readonly Channel<TelemetryReading> _channel =
        Channel.CreateBounded<TelemetryReading>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(TelemetryReading reading, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(reading, ct);

    /// Drains everything currently in the channel without blocking.
    public List<TelemetryReading> DrainAll(int max = 1000)
    {
        var batch = new List<TelemetryReading>();
        while (batch.Count < max && _channel.Reader.TryRead(out var item))
            batch.Add(item);
        return batch;
    }

    public int ApproxCount => _channel.Reader.Count;
}
