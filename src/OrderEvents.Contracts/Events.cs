namespace OrderEvents.Contracts;

/// <summary>
/// Published by the API once an order has been accepted and persisted.
/// </summary>
/// <param name="OrderId">Key the message is partitioned on, so all events for
/// one order land on the same partition and are processed in order.</param>
/// <param name="CorrelationId">Flows through every log line and follow-up event
/// so a single request can be traced across both services.</param>
public sealed record OrderPlaced(
    Guid OrderId,
    string CustomerId,
    string Sku,
    int Quantity,
    decimal TotalAmount,
    DateTimeOffset PlacedAt,
    string CorrelationId);

/// <summary>
/// Published by the worker once it has decided an order's outcome.
/// </summary>
public sealed record OrderProcessed(
    Guid OrderId,
    OrderStatus Status,
    string? Reason,
    DateTimeOffset ProcessedAt,
    string CorrelationId);
