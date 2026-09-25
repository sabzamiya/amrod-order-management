using FluentValidation;
using Amrod.OrderManagement.Api.DTOs;

namespace Amrod.OrderManagement.Api.Validators;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    private static readonly HashSet<string> SadcCountries =
    [
        "AO", "BW", "KM", "CD", "SZ", "LS",
        "MG", "MW", "MU", "MZ", "NA", "SC",
        "ZA", "TZ", "ZM", "ZW"
    ];

    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2)
            .Must(code => SadcCountries.Contains(code.ToUpperInvariant()))
            .WithMessage("CountryCode must be a valid SADC ISO country code.");
    }
}