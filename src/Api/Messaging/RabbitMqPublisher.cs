using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Amrod.OrderManagement.Api.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private const string ExchangeName = "amrod.orders";
    private const string QueueName = "order-created";
    private const string RoutingKey = "order.created";

    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IConfiguration configuration,
        ILogger<RabbitMqPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublishOrderCreatedAsync(
        OrderCreatedEvent message,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        _logger.LogInformation(
            "Connecting to RabbitMQ at {HostName}:{Port}",
            factory.HostName,
            factory.Port);

        await using var connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            QueueName,
            ExchangeName,
            routingKey: RoutingKey,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(message));

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            CorrelationId = correlationId
        };

        await channel.BasicPublishAsync(
            ExchangeName,
            RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published OrderCreated event for OrderId {OrderId} with CorrelationId {CorrelationId}",
            message.OrderId,
            correlationId);
    }
}