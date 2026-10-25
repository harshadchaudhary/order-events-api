using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderEvents.Infrastructure.Messaging;

/// <summary>
/// Base class for a Kafka consumer hosted as a background service.
///
/// Offsets are committed manually, only after <see cref="HandleAsync"/> returns
/// successfully. With auto-commit a crash mid-handler would skip the message:
/// the offset would already have moved on while the work never completed.
/// </summary>
public abstract class KafkaConsumerService<TMessage>(
    IOptions<KafkaOptions> options,
    ILogger logger) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected abstract string Topic { get; }

    protected abstract Task HandleAsync(TMessage message, CancellationToken ct);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consume() blocks, so the loop gets its own thread rather than
        // occupying one from the thread pool for the service's whole lifetime.
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogError("Kafka consumer error: {Reason}", e.Reason))
            .SetPartitionsAssignedHandler((_, partitions) =>
                logger.LogInformation("Assigned partitions: {Partitions}", string.Join(", ", partitions)))
            .Build();

        consumer.Subscribe(Topic);
        logger.LogInformation("Subscribed to {Topic} as group {GroupId}", Topic, _options.ConsumerGroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;

                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Consume failed: {Reason}", ex.Error.Reason);
                    continue;
                }

                if (result?.Message is null)
                {
                    continue;
                }

                try
                {
                    var message = JsonSerializer.Deserialize<TMessage>(result.Message.Value);
                    if (message is null)
                    {
                        // Nothing to retry — a null payload will never deserialize.
                        logger.LogWarning(
                            "Skipping empty payload at {Topic} [{Partition}] offset {Offset}",
                            result.Topic, result.Partition.Value, result.Offset.Value);
                    }
                    else
                    {
                        await HandleAsync(message, stoppingToken);
                    }

                    consumer.Commit(result);
                }
                catch (JsonException ex)
                {
                    // Poison message: committing moves past it so one bad
                    // payload cannot block the partition forever. A real system
                    // would forward this to a dead-letter topic first.
                    logger.LogError(
                        ex,
                        "Malformed payload at {Topic} [{Partition}] offset {Offset}; skipping",
                        result.Topic, result.Partition.Value, result.Offset.Value);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Transient failure: do NOT commit, so the message is
                    // redelivered after a rebalance or restart.
                    logger.LogError(
                        ex,
                        "Handler failed at {Topic} [{Partition}] offset {Offset}; leaving offset uncommitted",
                        result.Topic, result.Partition.Value, result.Offset.Value);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Consumer for {Topic} stopping", Topic);
        }
        finally
        {
            // Leave the group cleanly so the broker reassigns partitions
            // immediately instead of waiting for the session timeout.
            consumer.Close();
        }
    }
}
