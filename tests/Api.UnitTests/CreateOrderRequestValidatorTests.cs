using Amrod.OrderManagement.Api.DTOs;
using Amrod.OrderManagement.Api.Validators;

namespace Amrod.OrderManagement.Api.UnitTests;

public class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_ForValidSouthAfricanOrder()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "ZA",
            "ZAR",
            [
                new CreateOrderLineItemRequest(
                    "PROD-001",
                    "Test Product",
                    2,
                    100m)
            ]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_ForInvalidCountryCurrencyPair()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "ZA",
            "USD",
            [
                new CreateOrderLineItemRequest(
                    "PROD-001",
                    "Test Product",
                    1,
                    100m)
            ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "CurrencyCode");
    }

    [Fact]
    public void Validate_ShouldAllowZimbabweUsd()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "ZW",
            "USD",
            [
                new CreateOrderLineItemRequest(
                    "PROD-001",
                    "Test Product",
                    1,
                    100m)
            ]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLineItemsAreEmpty()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "ZA",
            "ZAR",
            []);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }
}