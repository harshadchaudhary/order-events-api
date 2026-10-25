using OrderEvents.Api.Models;

namespace OrderEvents.Tests;

public class CreateOrderRequestValidatorTests
{
    [Fact]
    public void Accepts_a_valid_request()
    {
        var errors = CreateOrderRequestValidator.Validate(
            new CreateOrderRequest("cust-1", "SKU-1", 2, 125m));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_customer_id(string customerId)
    {
        var errors = CreateOrderRequestValidator.Validate(
            new CreateOrderRequest(customerId, "SKU-1", 1, 10m));

        Assert.Contains(nameof(CreateOrderRequest.CustomerId), errors.Keys);
    }

    [Fact]
    public void Rejects_a_blank_sku()
    {
        var errors = CreateOrderRequestValidator.Validate(
            new CreateOrderRequest("cust-1", "", 1, 10m));

        Assert.Contains(nameof(CreateOrderRequest.Sku), errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_non_positive_quantity(int quantity)
    {
        var errors = CreateOrderRequestValidator.Validate(
            new CreateOrderRequest("cust-1", "SKU-1", quantity, 10m));

        Assert.Contains(nameof(CreateOrderRequest.Quantity), errors.Keys);
    }

    [Fact]
    public void Rejects_a_negative_unit_price()
    {
        var errors = CreateOrderRequestValidator.Validate(
            new CreateOrderRequest("cust-1", "SKU-1", 1, -0.01m));

        Assert.Contains(nameof(CreateOrderRequest.UnitPrice), errors.Keys);
    }

    [Fact]
    public void Reports_every_invalid_field_at_once()
    {
        var errors = CreateOrderRequestValidator.Validate(
            new CreateOrderRequest("", "", 0, -1m));

        Assert.Equal(4, errors.Count);
    }
}
