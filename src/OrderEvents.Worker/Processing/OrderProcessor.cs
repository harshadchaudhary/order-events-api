using OrderEvents.Contracts;

namespace OrderEvents.Worker.Processing;

public sealed record ProcessingDecision(OrderStatus Status, string? Reason);

/// <summary>
/// The order rules, kept as a pure function so they can be tested without a
/// broker, a database or a cache.
/// </summary>
public static class OrderProcessor
{
    public const int ManualApprovalThreshold = 100;
    public const decimal MaximumAutoApprovedAmount = 50_000m;

    public static ProcessingDecision Decide(OrderPlaced order)
    {
        if (order.Quantity <= 0)
        {
            return new ProcessingDecision(OrderStatus.Rejected, "Quantity must be greater than zero.");
        }

        if (order.TotalAmount <= 0)
        {
            return new ProcessingDecision(OrderStatus.Rejected, "Order total must be greater than zero.");
        }

        if (order.Quantity > ManualApprovalThreshold)
        {
            return new ProcessingDecision(
                OrderStatus.Rejected,
                $"Quantity above {ManualApprovalThreshold} requires manual approval.");
        }

        if (order.TotalAmount > MaximumAutoApprovedAmount)
        {
            return new ProcessingDecision(
                OrderStatus.Rejected,
                $"Order total above {MaximumAutoApprovedAmount:N0} requires manual approval.");
        }

        return new ProcessingDecision(OrderStatus.Confirmed, null);
    }
}
