using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OrderEvents.Infrastructure.Messaging;

/// <summary>
/// Stand-in publisher used when <c>Kafka:Enabled</c> is false. It logs what
/// would have been produced so the API can be run and explored without a
/// broker; nothing is actually published, so the worker sees no messages.
/// </summary>
public sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync<T>(string topic, string key, T message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Kafka disabled — would publish to {Topic} key {Key}: {Payload}",
            topic, key, JsonSerializer.Serialize(message));

        return Task.CompletedTask;
    }
}
