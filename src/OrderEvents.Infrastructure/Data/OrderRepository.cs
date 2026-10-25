using Microsoft.EntityFrameworkCore;
using OrderEvents.Contracts;

namespace OrderEvents.Infrastructure.Data;

public interface IOrderRepository
{
    Task<Order?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, int limit, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);

    /// <summary>
    /// Applies a processing outcome. Returns false when the order is missing or
    /// when this correlation id has already been applied, so a redelivered
    /// Kafka message is a no-op rather than a double write.
    /// </summary>
    Task<bool> TryApplyOutcomeAsync(
        Guid id,
        OrderStatus status,
        string? reason,
        DateTimeOffset processedAt,
        string correlationId,
        CancellationToken ct = default);
}

public sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(
        string customerId, int limit, CancellationToken ct = default) =>
        await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryApplyOutcomeAsync(
        Guid id,
        OrderStatus status,
        string? reason,
        DateTimeOffset processedAt,
        string correlationId,
        CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return false;
        }

        if (order.LastProcessedCorrelationId == correlationId)
        {
            // Already applied — this is a redelivery.
            return false;
        }

        order.Status = status;
        order.StatusReason = reason;
        order.ProcessedAt = processedAt;
        order.LastProcessedCorrelationId = correlationId;

        await db.SaveChangesAsync(ct);
        return true;
    }
}
