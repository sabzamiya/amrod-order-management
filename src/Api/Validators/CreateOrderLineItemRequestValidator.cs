using Amrod.OrderManagement.Api.DTOs;
using FluentValidation;

namespace Amrod.OrderManagement.Api.Validators;

public class CreateOrderLineItemRequestValidator
    : AbstractValidator<CreateOrderLineItemRequest>
{
    public CreateOrderLineItemRequestValidator()
    {
        RuleFor(x => x.ProductCode)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0);
    }
}