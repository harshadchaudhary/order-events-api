using Microsoft.EntityFrameworkCore;
using OrderEvents.Contracts;
using OrderEvents.Infrastructure.Data;

namespace OrderEvents.Tests;

public class OrderRepositoryTests
{
    private static OrderDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrderDbContext>()
            // A fresh database name per test keeps them independent.
            .UseInMemoryDatabase($"orders-{Guid.NewGuid():N}")
            .Options);

    private static Order NewOrder() => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = "cust-1",
        Sku = "SKU-1",
        Quantity = 2,
        TotalAmount = 250m,
        Status = OrderStatus.Pending,
        PlacedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Stores_and_reads_back_an_order()
    {
        await using var db = NewContext();
        var repository = new OrderRepository(db);
        var order = NewOrder();

        await repository.AddAsync(order);
        var found = await repository.GetAsync(order.Id);

        Assert.NotNull(found);
        Assert.Equal(order.CustomerId, found!.CustomerId);
        Assert.Equal(OrderStatus.Pending, found.Status);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_order()
    {
        await using var db = NewContext();
        var repository = new OrderRepository(db);

        Assert.Null(await repository.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Applies_a_processing_outcome()
    {
        await using var db = NewContext();
        var repository = new OrderRepository(db);
        var order = NewOrder();
        await repository.AddAsync(order);

        var applied = await repository.TryApplyOutcomeAsync(
            order.Id, OrderStatus.Confirmed, null, DateTimeOffset.UtcNow, "corr-1");

        Assert.True(applied);

        var updated = await repository.GetAsync(order.Id);
        Assert.Equal(OrderStatus.Confirmed, updated!.Status);
        Assert.NotNull(updated.ProcessedAt);
    }

    [Fact]
    public async Task Ignores_a_redelivery_of_the_same_correlation_id()
    {
        // Kafka is at-least-once, so the same OrderPlaced can arrive twice.
        await using var db = NewContext();
        var repository = new OrderRepository(db);
        var order = NewOrder();
        await repository.AddAsync(order);

        await repository.TryApplyOutcomeAsync(
            order.Id, OrderStatus.Confirmed, null, DateTimeOffset.UtcNow, "corr-1");

        var secondAttempt = await repository.TryApplyOutcomeAsync(
            order.Id, OrderStatus.Rejected, "changed my mind", DateTimeOffset.UtcNow, "corr-1");

        Assert.False(secondAttempt);

        var stored = await repository.GetAsync(order.Id);
        Assert.Equal(OrderStatus.Confirmed, stored!.Status);
        Assert.Null(stored.StatusReason);
    }

    [Fact]
    public async Task Does_not_apply_an_outcome_to_an_unknown_order()
    {
        await using var db = NewContext();
        var repository = new OrderRepository(db);

        var applied = await repository.TryApplyOutcomeAsync(
            Guid.NewGuid(), OrderStatus.Confirmed, null, DateTimeOffset.UtcNow, "corr-1");

        Assert.False(applied);
    }

    [Fact]
    public async Task Lists_a_customers_orders_newest_first()
    {
        await using var db = NewContext();
        var repository = new OrderRepository(db);

        var older = NewOrder();
        older.PlacedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var newer = NewOrder();
        newer.PlacedAt = DateTimeOffset.UtcNow;

        var otherCustomer = NewOrder();
        otherCustomer.CustomerId = "cust-2";

        await repository.AddAsync(older);
        await repository.AddAsync(newer);
        await repository.AddAsync(otherCustomer);

        var results = await repository.GetByCustomerAsync("cust-1", limit: 10);

        Assert.Equal(2, results.Count);
        Assert.Equal(newer.Id, results[0].Id);
        Assert.Equal(older.Id, results[1].Id);
    }
}
