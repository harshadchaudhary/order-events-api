using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderEvents.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(string topic, string key, T message, CancellationToken ct = default);
}

/// <summary>
/// Thin wrapper over a single long-lived Confluent producer. The producer is
/// registered as a singleton: creating one per request opens a new set of
/// broker connections every time and will exhaust sockets under load.
/// </summary>
public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = options.Value.Acks.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? Acks.All
                : Acks.Leader,
            MessageTimeoutMs = options.Value.MessageTimeoutMs,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error: {Reason}", e.Reason))
            .Build();
    }

    public async Task PublishAsync<T>(string topic, string key, T message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(message);

        var result = await _producer.ProduceAsync(
            topic,
            new Message<string, string> { Key = key, Value = payload },
            ct);

        _logger.LogInformation(
            "Published to {Topic} [{Partition}] offset {Offset} key {Key}",
            result.Topic, result.Partition.Value, result.Offset.Value, key);
    }

    public void Dispose()
    {
        // Flush before shutdown, otherwise buffered messages are dropped.
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
