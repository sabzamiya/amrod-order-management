using Amrod.OrderManagement.Api.Models;

namespace Amrod.OrderManagement.Api.DTOs;

public record CreateOrderLineItemRequest(
    string ProductCode,
    string Description,
    int Quantity,
    decimal UnitPrice
);

public record CreateOrderRequest(
    Guid CustomerId,
    string CountryCode,
    string CurrencyCode,
    List<CreateOrderLineItemRequest> LineItems
);

public record UpdateOrderStatusRequest(
    OrderStatus Status);

public record OrderLineItemResponse(
    Guid Id,
    string ProductCode,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

public record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string CountryCode,
    string CurrencyCode,
    OrderStatus Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<OrderLineItemResponse> LineItems
);