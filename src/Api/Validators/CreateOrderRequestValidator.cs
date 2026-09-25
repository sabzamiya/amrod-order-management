using Amrod.OrderManagement.Api.DTOs;
using FluentValidation;

namespace Amrod.OrderManagement.Api.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private static readonly Dictionary<string, HashSet<string>> CountryCurrencies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AO"] = ["AOA"],
            ["BW"] = ["BWP"],
            ["KM"] = ["KMF"],
            ["CD"] = ["CDF"],
            ["SZ"] = ["SZL", "ZAR"],
            ["LS"] = ["LSL", "ZAR"],
            ["MG"] = ["MGA"],
            ["MW"] = ["MWK"],
            ["MU"] = ["MUR"],
            ["MZ"] = ["MZN"],
            ["NA"] = ["NAD", "ZAR"],
            ["SC"] = ["SCR"],
            ["ZA"] = ["ZAR"],
            ["TZ"] = ["TZS"],
            ["ZM"] = ["ZMW"],
            ["ZW"] = ["ZWL", "USD"]
        };

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty();

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2)
            .Must(code => CountryCurrencies.ContainsKey(code))
            .WithMessage("CountryCode must be a valid SADC ISO country code.");

        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .Length(3)
            .Must((request, currency) =>
                CountryCurrencies.TryGetValue(request.CountryCode, out var currencies) &&
                currencies.Contains(currency))
            .WithMessage("CurrencyCode is not valid for the selected SADC country.");

        RuleFor(x => x.LineItems)
            .NotEmpty()
            .WithMessage("At least one order line item is required.");

        RuleForEach(x => x.LineItems)
            .SetValidator(new CreateOrderLineItemRequestValidator());
    }
}