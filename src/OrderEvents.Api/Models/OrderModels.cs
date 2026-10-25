using OrderEvents.Contracts;
using OrderEvents.Infrastructure.Data;

namespace OrderEvents.Api.Models;

public sealed record CreateOrderRequest(string CustomerId, string Sku, int Quantity, decimal UnitPrice);

public sealed record OrderResponse(
    Guid Id,
    string CustomerId,
    string Sku,
    int Quantity,
    decimal TotalAmount,
    string Status,
    string? StatusReason,
    DateTimeOffset PlacedAt,
    DateTimeOffset? ProcessedAt)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Sku,
        order.Quantity,
        order.TotalAmount,
        order.Status.ToString(),
        order.StatusReason,
        order.PlacedAt,
        order.ProcessedAt);
}

public static class CreateOrderRequestValidator
{
    /// <summary>
    /// Returns one entry per invalid field, in the shape
    /// <c>ValidationProblem</c> expects. Empty means valid.
    /// </summary>
    public static Dictionary<string, string[]> Validate(CreateOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            errors[nameof(request.CustomerId)] = ["Customer id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            errors[nameof(request.Sku)] = ["Sku is required."];
        }

        if (request.Quantity <= 0)
        {
            errors[nameof(request.Quantity)] = ["Quantity must be greater than zero."];
        }

        if (request.UnitPrice < 0)
        {
            errors[nameof(request.UnitPrice)] = ["Unit price cannot be negative."];
        }

        return errors;
    }
}
