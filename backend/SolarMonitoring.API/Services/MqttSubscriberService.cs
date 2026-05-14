using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// Long-running background service. Connects to HiveMQ, subscribes to the
/// configured topic, and pushes every received reading into MqttMessageQueue.
/// The Quartz job is what flushes that queue to MongoDB.
public class MqttSubscriberService : BackgroundService
{
    private readonly MqttSettings _settings;
    private readonly MqttMessageQueue _queue;
    private readonly ILogger<MqttSubscriberService> _logger;
    private IManagedMqttClient? _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public MqttSubscriberService(
        IOptions<MqttSettings> settings,
        MqttMessageQueue queue,
        ILogger<MqttSubscriberService> logger)
    {
        _settings = settings.Value;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _client = factory.CreateManagedMqttClient();

        // Stable ClientId + CleanSession=false makes the session persistent:
        // HiveMQ queues QoS≥1 messages for this ClientId while the API is offline
        // and replays them on reconnect, so a crash/restart doesn't drop telemetry.
        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.Host, _settings.Port)
            .WithClientId(_settings.ClientId)
            .WithCleanSession(false);

        if (!string.IsNullOrWhiteSpace(_settings.Username))
            clientOptions = clientOptions.WithCredentials(_settings.Username, _settings.Password);

        if (_settings.UseTls)
        {
            clientOptions = clientOptions.WithTlsOptions(o =>
            {
                o.UseTls(true);
                o.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13);
                // MQTTnet 4.3's default validator misbehaves on .NET 9 — delegate
                // to the standard SslPolicyErrors check (trusts the system store).
                o.WithCertificateValidationHandler(args =>
                {
                    if (args.SslPolicyErrors == SslPolicyErrors.None) return true;

                    // Inspect what MQTTnet's built-in chain build complained about.
                    foreach (var s in args.Chain.ChainStatus)
                        _logger.LogWarning("Initial chain status: {Status} - {Info}",
                            s.Status, s.StatusInformation.Trim());

                    if (args.Certificate is null) return false;

                    // Re-build the chain ourselves with revocation off
                    // (.NET on macOS often can't reach OCSP responders, and
                    // HiveMQ Cloud's Let's Encrypt cert is otherwise valid).
                    using var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.System;

                    var leaf = args.Certificate as X509Certificate2
                        ?? new X509Certificate2(args.Certificate);
                    var ok = chain.Build(leaf);

                    if (!ok)
                    {
                        foreach (var s in chain.ChainStatus)
                            _logger.LogError("Rebuild chain status: {Status} - {Info}",
                                s.Status, s.StatusInformation.Trim());
                    }
                    return ok;
                });
            });
        }

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptions.Build())
            .Build();

        _client.ApplicationMessageReceivedAsync += OnMessageReceived;
        _client.ConnectedAsync += async _ =>
        {
            _logger.LogInformation("MQTT connected to {Host}:{Port}", _settings.Host, _settings.Port);
            await Task.CompletedTask;
        };
        _client.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
            await Task.CompletedTask;
        };
        _client.ConnectingFailedAsync += async e =>
        {
            _logger.LogError(e.Exception, "MQTT connect failed");
            await Task.CompletedTask;
        };

        await _client.SubscribeAsync(new List<MqttTopicFilter>
        {
            new MqttTopicFilterBuilder()
                .WithTopic(_settings.Topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build()
        });

        await _client.StartAsync(managedOptions);
        _logger.LogInformation("MQTT subscriber started, topic={Topic}", _settings.Topic);

        // Keep service alive until cancellation.
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (TaskCanceledException) { }
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var payload = e.ApplicationMessage.PayloadSegment.Count > 0
                ? Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)
                : string.Empty;

            if (string.IsNullOrWhiteSpace(payload)) return;

            var reading = JsonSerializer.Deserialize<TelemetryReading>(payload, JsonOpts);
            if (reading is null)
            {
                _logger.LogWarning("MQTT payload deserialized to null on topic {Topic}", e.ApplicationMessage.Topic);
                return;
            }

            if (reading.Timestamp == default)
                reading.Timestamp = DateTime.UtcNow;

            await _queue.EnqueueAsync(reading);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse MQTT payload on topic {Topic}", e.ApplicationMessage.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing MQTT message");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            await _client.StopAsync();
            _client.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }
}
