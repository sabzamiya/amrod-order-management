using Amrod.OrderManagement.Api.Messaging;

namespace Amrod.OrderManagement.Api.IntegrationTests;

public class FakeRabbitMqPublisher : IRabbitMqPublisher
{
    private readonly List<OrderCreatedEvent> _publishedMessages = [];

    public IReadOnlyList<OrderCreatedEvent> PublishedMessages =>
        _publishedMessages;

    public Task PublishOrderCreatedAsync(
        OrderCreatedEvent message,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _publishedMessages.Add(message);

        return Task.CompletedTask;
    }
}