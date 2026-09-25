using System.Text;
using System.Text.Json;
using Amrod.OrderManagement.Api.Data;
using Amrod.OrderManagement.Api.Models;
using Amrod.OrderManagement.Worker.Messaging;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Amrod.OrderManagement.Worker;

public class Worker : BackgroundService
{
    private const string ExchangeName = "amrod.orders";

    private const string QueueName = "order-created";
    private const string RoutingKey = "order.created";

    private const string RetryQueueName = "order-created-retry";
    private const string RetryRoutingKey = "order.created.retry";

    private const string DeadLetterQueueName = "order-created-dead-letter";
    private const string DeadLetterRoutingKey = "order.created.dead-letter";

    private const string RetryHeaderName = "x-retry-count";

    private const int MaxRetryCount = 3;
    private const int RetryDelayMilliseconds = 5000;

    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    private IConnection? _connection;
    private IChannel? _channel;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RabbitMQ Worker ExecuteAsync started");

        var factory = new ConnectionFactory
        {
            HostName =
                _configuration["RabbitMQ:HostName"]
                ?? "localhost",

            Port = int.Parse(
                _configuration["RabbitMQ:Port"]
                ?? "5672"),

            UserName =
                _configuration["RabbitMQ:UserName"]
                ?? "guest",

            Password =
                _configuration["RabbitMQ:Password"]
                ?? "guest"
        };

        _connection =
            await factory.CreateConnectionAsync(
                stoppingToken);

        _logger.LogInformation(
            "Connected to RabbitMQ at {HostName}:{Port}",
            factory.HostName,
            factory.Port);

        _channel =
            await _connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await ConfigureRabbitMqAsync(
            stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer =
            new AsyncEventingBasicConsumer(
                _channel);

        consumer.ReceivedAsync +=
            async (_, eventArgs) =>
            {
                await HandleMessageAsync(
                    eventArgs,
                    stoppingToken);
            };

        await _channel.BasicConsumeAsync(
            QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "RabbitMQ consumer registered on queue {QueueName}",
            QueueName);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

    private async Task ConfigureRabbitMqAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException(
                "RabbitMQ channel is not available.");
        }

        /*
         * Main exchange.
         */
        await _channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Direct,
            durable: true,
            cancellationToken:
                cancellationToken);

        /*
         * Existing main queue.
         *
         * We deliberately keep this declaration
         * identical to the API publisher.
         *
         * No new arguments are added here because
         * changing arguments on an existing RabbitMQ
         * queue would cause PRECONDITION_FAILED.
         */
        await _channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken:
                cancellationToken);

        await _channel.QueueBindAsync(
            QueueName,
            ExchangeName,
            RoutingKey,
            cancellationToken:
                cancellationToken);

        /*
         * Retry queue.
         *
         * Failed messages are explicitly published
         * here by the Worker.
         *
         * RabbitMQ holds them for 5 seconds.
         * When the TTL expires, RabbitMQ dead-letters
         * them back to:
         *
         * amrod.orders / order.created
         *
         * which routes them back to order-created.
         */
        var retryQueueArguments =
            new Dictionary<string, object?>
            {
                ["x-message-ttl"] =
                    RetryDelayMilliseconds,

                ["x-dead-letter-exchange"] =
                    ExchangeName,

                ["x-dead-letter-routing-key"] =
                    RoutingKey
            };

        await _channel.QueueDeclareAsync(
            RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments:
                retryQueueArguments,
            cancellationToken:
                cancellationToken);

        await _channel.QueueBindAsync(
            RetryQueueName,
            ExchangeName,
            RetryRoutingKey,
            cancellationToken:
                cancellationToken);

        /*
         * Dead-letter queue.
         *
         * Messages arrive here after the configured
         * maximum number of retries is exhausted.
         */
        await _channel.QueueDeclareAsync(
            DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken:
                cancellationToken);

        await _channel.QueueBindAsync(
            DeadLetterQueueName,
            ExchangeName,
            DeadLetterRoutingKey,
            cancellationToken:
                cancellationToken);

        _logger.LogInformation(
            "RabbitMQ topology configured. MainQueue={MainQueue}, RetryQueue={RetryQueue}, DeadLetterQueue={DeadLetterQueue}",
            QueueName,
            RetryQueueName,
            DeadLetterQueueName);
    }

    private async Task HandleMessageAsync(
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            return;
        }

        var correlationId =
            eventArgs.BasicProperties
                .CorrelationId
            ?? Guid.NewGuid().ToString();

        using var logScope =
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["CorrelationId"] =
                        correlationId
                });

        try
        {
            var json =
                Encoding.UTF8.GetString(
                    eventArgs.Body.ToArray());

            var message =
                JsonSerializer.Deserialize
                    <OrderCreatedEvent>(json);

            if (message is null)
            {
                throw new InvalidOperationException(
                    "Invalid OrderCreated message.");
            }

            var retryCount =
                GetRetryCount(
                    eventArgs.BasicProperties);

            _logger.LogInformation(
                "Processing OrderCreated event for OrderId {OrderId}. Retry attempt {RetryCount}/{MaxRetryCount}",
                message.OrderId,
                retryCount,
                MaxRetryCount);

            using var scope =
                _scopeFactory.CreateScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService
                        <OrderManagementDbContext>();

            var order =
                await db.Orders
                    .FirstOrDefaultAsync(
                        o =>
                            o.Id ==
                            message.OrderId,
                        cancellationToken);

            if (order is null)
            {
                /*
                 * We treat a missing order as a
                 * processing failure rather than
                 * acknowledging it permanently.
                 *
                 * This also gives us a clean way
                 * to verify retry/DLQ behaviour.
                 */
                throw new InvalidOperationException(
                    $"Order {message.OrderId} was not found.");
            }

            /*
             * Idempotency.
             *
             * RabbitMQ uses at-least-once delivery.
             * A message may therefore be delivered
             * more than once.
             */
            if (
                order.Status ==
                OrderStatus.Fulfilled)
            {
                _logger.LogInformation(
                    "Order {OrderId} is already fulfilled. Message will be acknowledged without duplicate processing.",
                    order.Id);

                await _channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken:
                        cancellationToken);

                return;
            }

            /*
             * Simulate downstream processing.
             */
            await Task.Delay(
                1000,
                cancellationToken);

            order.Status =
                OrderStatus.Fulfilled;

            order.UpdatedAt =
                DateTime.UtcNow;

            await db.SaveChangesAsync(
                cancellationToken);

            await _channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken:
                    cancellationToken);

            _logger.LogInformation(
                "Order {OrderId} transitioned to Fulfilled.",
                order.Id);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            _logger.LogInformation(
                "RabbitMQ message processing cancelled because the Worker is stopping.");

            throw;
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(
                eventArgs,
                correlationId,
                ex,
                cancellationToken);
        }
    }

    private async Task HandleFailureAsync(
        BasicDeliverEventArgs eventArgs,
        string correlationId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            return;
        }

        var currentRetryCount =
            GetRetryCount(
                eventArgs.BasicProperties);

        if (
            currentRetryCount <
            MaxRetryCount)
        {
            var nextRetryCount =
                currentRetryCount + 1;

            _logger.LogWarning(
                exception,
                "OrderCreated processing failed. Scheduling retry {RetryCount}/{MaxRetryCount} after {RetryDelayMilliseconds} ms.",
                nextRetryCount,
                MaxRetryCount,
                RetryDelayMilliseconds);

            var retryProperties =
                CreateProperties(
                    eventArgs.BasicProperties,
                    correlationId,
                    nextRetryCount);

            /*
             * Publish the original message body
             * into the retry queue.
             */
            await _channel.BasicPublishAsync(
                ExchangeName,
                RetryRoutingKey,
                mandatory: false,
                basicProperties:
                    retryProperties,
                body: eventArgs.Body,
                cancellationToken:
                    cancellationToken);

            /*
             * Only ACK the original delivery after
             * the retry copy has been published.
             */
            await _channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken:
                    cancellationToken);

            return;
        }

        _logger.LogError(
            exception,
            "OrderCreated processing failed after {MaxRetryCount} retries. Moving message to dead-letter queue.",
            MaxRetryCount);

        var deadLetterProperties =
            CreateProperties(
                eventArgs.BasicProperties,
                correlationId,
                currentRetryCount);

        await _channel.BasicPublishAsync(
            ExchangeName,
            DeadLetterRoutingKey,
            mandatory: false,
            basicProperties:
                deadLetterProperties,
            body: eventArgs.Body,
            cancellationToken:
                cancellationToken);

        /*
         * ACK the original message because its
         * durable copy is now in our DLQ.
         */
        await _channel.BasicAckAsync(
            eventArgs.DeliveryTag,
            multiple: false,
            cancellationToken:
                cancellationToken);
    }

    private static int GetRetryCount(
        IReadOnlyBasicProperties properties)
    {
        if (
            properties.Headers is null ||
            !properties.Headers.TryGetValue(
                RetryHeaderName,
                out var value) ||
            value is null)
        {
            return 0;
        }

        return value switch
        {
            int intValue =>
                intValue,

            long longValue =>
                checked((int)longValue),

            byte byteValue =>
                byteValue,

            byte[] bytes
                when int.TryParse(
                    Encoding.UTF8.GetString(
                        bytes),
                    out var parsedValue) =>
                parsedValue,

            _ => 0
        };
    }

    private static BasicProperties
        CreateProperties(
            IReadOnlyBasicProperties original,
            string correlationId,
            int retryCount)
    {
        var headers =
            new Dictionary<string, object?>();

        if (
            original.Headers is not null)
        {
            foreach (
                var header
                in original.Headers)
            {
                headers[header.Key] =
                    header.Value;
            }
        }

        headers[RetryHeaderName] =
            retryCount;

        return new BasicProperties
        {
            Persistent = true,

            ContentType =
                original.ContentType
                ?? "application/json",

            ContentEncoding =
                original.ContentEncoding,

            CorrelationId =
                correlationId,

            MessageId =
                original.MessageId,

            Type =
                original.Type,

            AppId =
                original.AppId,

            Headers =
                headers
        };
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await base.StopAsync(
            cancellationToken);
    }
}