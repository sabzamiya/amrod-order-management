namespace Amrod.OrderManagement.Api.DTOs;

public record CreateCustomerRequest(
    string Name,
    string Email,
    string CountryCode
);

public record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string CountryCode,
    DateTime CreatedAt
);