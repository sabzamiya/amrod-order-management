namespace Amrod.OrderManagement.Api.DTOs;

public record TokenRequest(string Permission);

public record TokenResponse(
    string AccessToken,
    DateTime ExpiresAt
);