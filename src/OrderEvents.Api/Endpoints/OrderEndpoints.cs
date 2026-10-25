using OrderEvents.Api.Models;
using OrderEvents.Contracts;
using OrderEvents.Infrastructure.Caching;
using OrderEvents.Infrastructure.Data;
using OrderEvents.Infrastructure.Messaging;

namespace OrderEvents.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders");

        orders.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Accepts an order, stores it as Pending and publishes OrderPlaced to Kafka.");

        orders.MapGet("/{id:guid}", GetOrder)
            .WithName("GetOrder")
            .WithSummary("Reads an order through the Redis cache, falling back to the database.");

        app.MapGet("/api/customers/{customerId}/orders", GetCustomerOrders)
            .WithTags("Orders")
            .WithName("GetCustomerOrders")
            .WithSummary("Lists a customer's most recent orders.");
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request,
        IOrderRepository repository,
        IOrderCache cache,
        IEventPublisher publisher,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var errors = CreateOrderRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var correlationId = Guid.NewGuid().ToString("N");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Sku = request.Sku,
            Quantity = request.Quantity,
            TotalAmount = request.UnitPrice * request.Quantity,
            Status = OrderStatus.Pending,
            PlacedAt = DateTimeOffset.UtcNow
        };

        // Persist before publishing. The reverse order can emit an event for an
        // order that was never stored, which the consumer then cannot find.
        await repository.AddAsync(order, ct);

        await publisher.PublishAsync(
            KafkaTopics.OrderPlaced,
            order.Id.ToString(),
            new OrderPlaced(
                order.Id,
                order.CustomerId,
                order.Sku,
                order.Quantity,
                order.TotalAmount,
                order.PlacedAt,
                correlationId),
            ct);

        await cache.SetAsync(order, ct);

        logger.LogInformation(
            "Order {OrderId} accepted for customer {CustomerId} (correlation {CorrelationId})",
            order.Id, order.CustomerId, correlationId);

        return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
    }

    private static async Task<IResult> GetOrder(
        Guid id,
        IOrderRepository repository,
        IOrderCache cache,
        CancellationToken ct)
    {
        // Cache-aside: try Redis, fall back to the database, then populate.
        var cached = await cache.GetAsync(id, ct);
        if (cached is not null)
        {
            return Results.Ok(OrderResponse.From(cached));
        }

        var order = await repository.GetAsync(id, ct);
        if (order is null)
        {
            return Results.NotFound();
        }

        await cache.SetAsync(order, ct);
        return Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> GetCustomerOrders(
        string customerId,
        IOrderRepository repository,
        CancellationToken ct,
        int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);

        var orders = await repository.GetByCustomerAsync(customerId, limit, ct);
        return Results.Ok(orders.Select(OrderResponse.From));
    }
}
