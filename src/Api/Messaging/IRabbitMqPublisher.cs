namespace Amrod.OrderManagement.Api.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishOrderCreatedAsync(
        OrderCreatedEvent message,
        string correlationId,
        CancellationToken cancellationToken = default);
}