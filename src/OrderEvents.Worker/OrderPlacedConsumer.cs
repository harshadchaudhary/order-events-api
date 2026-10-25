using Microsoft.Extensions.Options;
using OrderEvents.Contracts;
using OrderEvents.Infrastructure;
using OrderEvents.Infrastructure.Caching;
using OrderEvents.Infrastructure.Data;
using OrderEvents.Infrastructure.Messaging;
using OrderEvents.Worker.Processing;

namespace OrderEvents.Worker;

/// <summary>
/// Consumes <see cref="OrderPlaced"/>, decides the outcome, writes it to the
/// database, invalidates the cached copy and publishes
/// <see cref="OrderProcessed"/>.
/// </summary>
public sealed class OrderPlacedConsumer(
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    IOrderCache cache,
    ILogger<OrderPlacedConsumer> logger) : KafkaConsumerService<OrderPlaced>(options, logger)
{
    protected override string Topic => KafkaTopics.OrderPlaced;

    protected override async Task HandleAsync(OrderPlaced message, CancellationToken ct)
    {
        var decision = OrderProcessor.Decide(message);
        var processedAt = DateTimeOffset.UtcNow;

        // The repository is scoped (it owns a DbContext), so the long-lived
        // consumer resolves a fresh scope per message instead of holding one
        // DbContext open for the life of the service.
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var applied = await repository.TryApplyOutcomeAsync(
            message.OrderId,
            decision.Status,
            decision.Reason,
            processedAt,
            message.CorrelationId,
            ct);

        if (!applied)
        {
            logger.LogInformation(
                "Order {OrderId} not updated — unknown order or already processed under correlation {CorrelationId}",
                message.OrderId, message.CorrelationId);
            return;
        }

        // Invalidate before announcing, so anyone reacting to the event that
        // immediately reads the order cannot be served the pre-processing copy.
        await cache.InvalidateAsync(message.OrderId, ct);

        await publisher.PublishAsync(
            KafkaTopics.OrderProcessed,
            message.OrderId.ToString(),
            new OrderProcessed(
                message.OrderId,
                decision.Status,
                decision.Reason,
                processedAt,
                message.CorrelationId),
            ct);

        logger.LogInformation(
            "Order {OrderId} -> {Status} (correlation {CorrelationId})",
            message.OrderId, decision.Status, message.CorrelationId);
    }
}
