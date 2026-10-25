using OrderEvents.Contracts;
using OrderEvents.Worker.Processing;

namespace OrderEvents.Tests;

public class OrderProcessorTests
{
    private static OrderPlaced Order(int quantity = 1, decimal total = 100m) => new(
        OrderId: Guid.NewGuid(),
        CustomerId: "cust-1",
        Sku: "SKU-1",
        Quantity: quantity,
        TotalAmount: total,
        PlacedAt: DateTimeOffset.UtcNow,
        CorrelationId: "corr-1");

    [Fact]
    public void Confirms_a_normal_order()
    {
        var decision = OrderProcessor.Decide(Order(quantity: 2, total: 500m));

        Assert.Equal(OrderStatus.Confirmed, decision.Status);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void Confirms_an_order_exactly_on_the_quantity_threshold()
    {
        var decision = OrderProcessor.Decide(
            Order(quantity: OrderProcessor.ManualApprovalThreshold, total: 1_000m));

        Assert.Equal(OrderStatus.Confirmed, decision.Status);
    }

    [Fact]
    public void Rejects_quantity_above_the_manual_approval_threshold()
    {
        var decision = OrderProcessor.Decide(
            Order(quantity: OrderProcessor.ManualApprovalThreshold + 1, total: 1_000m));

        Assert.Equal(OrderStatus.Rejected, decision.Status);
        Assert.Contains("manual approval", decision.Reason);
    }

    [Fact]
    public void Confirms_an_order_exactly_on_the_amount_ceiling()
    {
        var decision = OrderProcessor.Decide(
            Order(quantity: 1, total: OrderProcessor.MaximumAutoApprovedAmount));

        Assert.Equal(OrderStatus.Confirmed, decision.Status);
    }

    [Fact]
    public void Rejects_a_total_above_the_amount_ceiling()
    {
        var decision = OrderProcessor.Decide(
            Order(quantity: 1, total: OrderProcessor.MaximumAutoApprovedAmount + 0.01m));

        Assert.Equal(OrderStatus.Rejected, decision.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_a_non_positive_quantity(int quantity)
    {
        var decision = OrderProcessor.Decide(Order(quantity: quantity, total: 100m));

        Assert.Equal(OrderStatus.Rejected, decision.Status);
    }

    [Fact]
    public void Rejects_a_zero_total()
    {
        var decision = OrderProcessor.Decide(Order(quantity: 1, total: 0m));

        Assert.Equal(OrderStatus.Rejected, decision.Status);
    }
}
