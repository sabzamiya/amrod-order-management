using Amrod.OrderManagement.Api.Data;
using Amrod.OrderManagement.Api.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amrod.OrderManagement.Api.IntegrationTests;

public class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName =
        $"AmrodIntegrationTests-{Guid.NewGuid()}";

    public FakeRabbitMqPublisher RabbitMqPublisher { get; } =
        new();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            /*
             * Replace the production SQL Server
             * DbContext with an isolated in-memory
             * database.
             */
            services.RemoveAll<
                DbContextOptions<OrderManagementDbContext>>();

            services.RemoveAll<
                OrderManagementDbContext>();

            services.AddDbContext<OrderManagementDbContext>(
                options =>
                {
                    options.UseInMemoryDatabase(
                        _databaseName);
                });

            /*
             * Replace the real RabbitMQ publisher.
             *
             * Integration tests must not depend on
             * an external RabbitMQ instance.
             */
            services.RemoveAll<IRabbitMqPublisher>();

            services.AddSingleton<IRabbitMqPublisher>(
                RabbitMqPublisher);

            using var serviceProvider =
                services.BuildServiceProvider();

            using var scope =
                serviceProvider.CreateScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<
                        OrderManagementDbContext>();

            db.Database.EnsureCreated();
        });
    }
}