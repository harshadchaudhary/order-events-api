using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderEvents.Infrastructure.Data;
using StackExchange.Redis;

// StackExchange.Redis also defines an `Order` type, so pin the name to ours.
using Order = OrderEvents.Infrastructure.Data.Order;

namespace OrderEvents.Infrastructure.Caching;

public interface IOrderCache
{
    Task<Order?> GetAsync(Guid id, CancellationToken ct = default);
    Task SetAsync(Order order, CancellationToken ct = default);

    /// <summary>
    /// Drops the cached copy. Called by the worker after it changes an order,
    /// so the next read repopulates from the database instead of serving the
    /// stale pre-processing state.
    /// </summary>
    Task InvalidateAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Cache-aside over Redis. Every Redis call is wrapped: if the cache is down
/// the request still succeeds from the database, just slower. A cache outage
/// should degrade latency, not availability.
/// </summary>
public sealed class RedisOrderCache(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisOrderCache> logger) : IOrderCache
{
    private readonly RedisOptions _options = options.Value;

    private static string KeyFor(Guid id) => $"order:{id:N}";

    public async Task<Order?> GetAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(KeyFor(id));
            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<Order>(value!);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis read failed for order {OrderId}; falling back to the database", id);
            return null;
        }
    }

    public async Task SetAsync(Order order, CancellationToken ct = default)
    {
        try
        {
            await redis.GetDatabase().StringSetAsync(
                KeyFor(order.Id),
                JsonSerializer.Serialize(order),
                TimeSpan.FromSeconds(_options.CacheSeconds));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis write failed for order {OrderId}; continuing uncached", order.Id);
        }
    }

    public async Task InvalidateAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(KeyFor(id));
        }
        catch (Exception ex)
        {
            // Worth a warning rather than a swallow: a failed invalidation means
            // readers see the old status until the TTL expires.
            logger.LogWarning(ex, "Redis invalidation failed for order {OrderId}; stale until TTL expiry", id);
        }
    }
}

/// <summary>Used when Redis is switched off in configuration.</summary>
public sealed class NullOrderCache : IOrderCache
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Order?>(null);
    public Task SetAsync(Order order, CancellationToken ct = default) => Task.CompletedTask;
    public Task InvalidateAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
}
