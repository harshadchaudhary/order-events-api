using OrderEvents.Contracts;

namespace OrderEvents.Infrastructure.Data;

public sealed class Order
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? StatusReason { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>
    /// Guards against the same Kafka message being applied twice. Kafka gives
    /// at-least-once delivery, so a redelivery after a crash must not overwrite
    /// a newer result.
    /// </summary>
    public string? LastProcessedCorrelationId { get; set; }
}
