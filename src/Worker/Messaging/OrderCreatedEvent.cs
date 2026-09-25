namespace Amrod.OrderManagement.Worker.Messaging;

public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string CountryCode,
    string CurrencyCode,
    DateTime CreatedAt);