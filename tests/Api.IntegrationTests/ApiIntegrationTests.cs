using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amrod.OrderManagement.Api.DTOs;
using Amrod.OrderManagement.Api.Models;

namespace Amrod.OrderManagement.Api.IntegrationTests;

public class ApiIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    public ApiIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Orders_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response =
            await client.GetAsync("/api/orders");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ReadToken_CanReadOrders()
    {
        using var client = _factory.CreateClient();

        var token =
            await GetTokenAsync(
                client,
                "Orders.Read");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await client.GetAsync("/api/orders");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task ReadToken_CannotCreateCustomer()
    {
        using var client = _factory.CreateClient();

        var token =
            await GetTokenAsync(
                client,
                "Orders.Read");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var request =
            new CreateCustomerRequest(
                "Integration Test Customer",
                $"readonly-{Guid.NewGuid()}@example.com",
                "ZA");

        var response =
            await client.PostAsJsonAsync(
                "/api/customers",
                request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task WriteToken_CanCreateAndRetrieveCustomer()
    {
        using var client = _factory.CreateClient();

        var token =
            await GetTokenAsync(
                client,
                "Orders.Write");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var email =
            $"integration-{Guid.NewGuid()}@example.com";

        var request =
            new CreateCustomerRequest(
                "Integration Customer",
                email,
                "ZA");

        var createResponse =
            await client.PostAsJsonAsync(
                "/api/customers",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var customer =
            await createResponse.Content
                .ReadFromJsonAsync<CustomerResponse>();

        Assert.NotNull(customer);

        Assert.Equal(
            "Integration Customer",
            customer.Name);

        Assert.Equal(
            email,
            customer.Email);

        Assert.Equal(
            "ZA",
            customer.CountryCode);

        var getResponse =
            await client.GetAsync(
                $"/api/customers/{customer.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var retrievedCustomer =
            await getResponse.Content
                .ReadFromJsonAsync<CustomerResponse>();

        Assert.NotNull(retrievedCustomer);

        Assert.Equal(
            customer.Id,
            retrievedCustomer.Id);

        Assert.Equal(
            email,
            retrievedCustomer.Email);
    }

    [Fact]
    public async Task CreateOrder_CalculatesTotal_AndPublishesEvent()
    {
        using var client = _factory.CreateClient();

        await AuthenticateAsync(
            client,
            "Orders.Write");

        var customer =
            await CreateCustomerAsync(client);

        var publishedBefore =
            _factory.RabbitMqPublisher
                .PublishedMessages.Count;

        var request =
            new CreateOrderRequest(
                customer.Id,
                "ZA",
                "ZAR",
                new List<CreateOrderLineItemRequest>
                {
                    new(
                        "SKU-001",
                        "Integration Product One",
                        2,
                        100m),

                    new(
                        "SKU-002",
                        "Integration Product Two",
                        3,
                        50m)
                });

        var response =
            await client.PostAsJsonAsync(
                "/api/orders",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>(
                    JsonOptions);

        Assert.NotNull(order);

        Assert.Equal(
            OrderStatus.Pending,
            order.Status);

        // Server-calculated total:
        // (2 x 100) + (3 x 50) = 350.
        Assert.Equal(
            350m,
            order.TotalAmount);

        Assert.Equal(
            2,
            order.LineItems.Count);

        Assert.Equal(
            publishedBefore + 1,
            _factory.RabbitMqPublisher
                .PublishedMessages.Count);

        var publishedEvent =
            _factory.RabbitMqPublisher
                .PublishedMessages.Last();

        Assert.Equal(
            order.Id,
            publishedEvent.OrderId);

        Assert.Equal(
            customer.Id,
            publishedEvent.CustomerId);

        Assert.Equal(
            350m,
            publishedEvent.TotalAmount);

        Assert.Equal(
            "ZA",
            publishedEvent.CountryCode);

        Assert.Equal(
            "ZAR",
            publishedEvent.CurrencyCode);
    }

    [Fact]
    public async Task OrderStatusUpdate_IsIdempotent()
    {
        using var client = _factory.CreateClient();

        await AuthenticateAsync(
            client,
            "Orders.Write");

        var customer =
            await CreateCustomerAsync(client);

        var order =
            await CreateOrderAsync(
                client,
                customer.Id);

        Assert.Equal(
            OrderStatus.Pending,
            order.Status);

        const string idempotencyKey =
            "integration-paid-key";

        var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/orders/{order.Id}/status");

        firstRequest.Headers.Add(
            "Idempotency-Key",
            idempotencyKey);

        firstRequest.Content =
            JsonContent.Create(
                new UpdateOrderStatusRequest(
                    OrderStatus.Paid),
                options: JsonOptions);

        var firstResponse =
            await client.SendAsync(firstRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        var firstResult =
            await firstResponse.Content
                .ReadFromJsonAsync<OrderResponse>(
                    JsonOptions);

        Assert.NotNull(firstResult);

        Assert.Equal(
            OrderStatus.Paid,
            firstResult.Status);

        /*
         * Repeat the same request using exactly
         * the same idempotency key.
         */
        var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/orders/{order.Id}/status");

        secondRequest.Headers.Add(
            "Idempotency-Key",
            idempotencyKey);

        secondRequest.Content =
            JsonContent.Create(
                new UpdateOrderStatusRequest(
                    OrderStatus.Paid),
                options: JsonOptions);

        var secondResponse =
            await client.SendAsync(secondRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode);

        var secondResult =
            await secondResponse.Content
                .ReadFromJsonAsync<OrderResponse>(
                    JsonOptions);

        Assert.NotNull(secondResult);

        Assert.Equal(
            OrderStatus.Paid,
            secondResult.Status);

        Assert.Equal(
            firstResult.Id,
            secondResult.Id);

        Assert.Equal(
            firstResult.UpdatedAt,
            secondResult.UpdatedAt);
    }

    [Fact]
    public async Task Order_CanTransition_Pending_ToPaid_ToFulfilled()
    {
        using var client = _factory.CreateClient();

        await AuthenticateAsync(
            client,
            "Orders.Write");

        var customer =
            await CreateCustomerAsync(client);

        var order =
            await CreateOrderAsync(
                client,
                customer.Id);

        Assert.Equal(
            OrderStatus.Pending,
            order.Status);

        var paid =
            await UpdateStatusAsync(
                client,
                order.Id,
                OrderStatus.Paid,
                $"paid-{Guid.NewGuid()}");

        Assert.Equal(
            OrderStatus.Paid,
            paid.Status);

        var fulfilled =
            await UpdateStatusAsync(
                client,
                order.Id,
                OrderStatus.Fulfilled,
                $"fulfilled-{Guid.NewGuid()}");

        Assert.Equal(
            OrderStatus.Fulfilled,
            fulfilled.Status);
    }

    [Fact]
    public async Task FulfilledOrder_CannotTransitionBackToPaid()
    {
        using var client = _factory.CreateClient();

        await AuthenticateAsync(
            client,
            "Orders.Write");

        var customer =
            await CreateCustomerAsync(client);

        var order =
            await CreateOrderAsync(
                client,
                customer.Id);

        await UpdateStatusAsync(
            client,
            order.Id,
            OrderStatus.Paid,
            $"paid-{Guid.NewGuid()}");

        await UpdateStatusAsync(
            client,
            order.Id,
            OrderStatus.Fulfilled,
            $"fulfilled-{Guid.NewGuid()}");

        var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/orders/{order.Id}/status");

        request.Headers.Add(
            "Idempotency-Key",
            $"invalid-{Guid.NewGuid()}");

        request.Content =
            JsonContent.Create(
                new UpdateOrderStatusRequest(
                    OrderStatus.Paid),
                options: JsonOptions);

        var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task StatusUpdate_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        await AuthenticateAsync(
            client,
            "Orders.Write");

        var customer =
            await CreateCustomerAsync(client);

        var order =
            await CreateOrderAsync(
                client,
                customer.Id);

        var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/orders/{order.Id}/status")
            {
                Content =
                    JsonContent.Create(
                        new UpdateOrderStatusRequest(
                            OrderStatus.Paid),
                        options: JsonOptions)
            };

        var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private static async Task AuthenticateAsync(
        HttpClient client,
        string permission)
    {
        var token =
            await GetTokenAsync(
                client,
                permission);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);
    }

    private static async Task<CustomerResponse>
        CreateCustomerAsync(
            HttpClient client)
    {
        var request =
            new CreateCustomerRequest(
                "Order Test Customer",
                $"order-test-{Guid.NewGuid()}@example.com",
                "ZA");

        var response =
            await client.PostAsJsonAsync(
                "/api/customers",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var customer =
            await response.Content
                .ReadFromJsonAsync<CustomerResponse>();

        Assert.NotNull(customer);

        return customer;
    }

    private static async Task<OrderResponse>
        CreateOrderAsync(
            HttpClient client,
            Guid customerId)
    {
        var request =
            new CreateOrderRequest(
                customerId,
                "ZA",
                "ZAR",
                new List<CreateOrderLineItemRequest>
                {
                    new(
                        "SKU-TEST",
                        "Integration Test Product",
                        2,
                        125m)
                });

        var response =
            await client.PostAsJsonAsync(
                "/api/orders",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>(
                    JsonOptions);

        Assert.NotNull(order);

        return order;
    }

    private static async Task<OrderResponse>
        UpdateStatusAsync(
            HttpClient client,
            Guid orderId,
            OrderStatus status,
            string idempotencyKey)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/orders/{orderId}/status");

        request.Headers.Add(
            "Idempotency-Key",
            idempotencyKey);

        request.Content =
            JsonContent.Create(
                new UpdateOrderStatusRequest(
                    status),
                options: JsonOptions);

        var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>(
                    JsonOptions);

        Assert.NotNull(order);

        return order;
    }

    private static async Task<string> GetTokenAsync(
        HttpClient client,
        string permission)
    {
        var response =
            await client.PostAsJsonAsync(
                "/api/auth/token",
                new TokenRequest(permission));

        response.EnsureSuccessStatusCode();

        var tokenResponse =
            await response.Content
                .ReadFromJsonAsync<TokenResponse>();

        Assert.NotNull(tokenResponse);

        Assert.False(
            string.IsNullOrWhiteSpace(
                tokenResponse.AccessToken));

        return tokenResponse.AccessToken;
    }
}