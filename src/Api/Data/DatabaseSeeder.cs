using Amrod.OrderManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Amrod.OrderManagement.Api.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        OrderManagementDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (await db.Customers.AnyAsync(
            c => c.Email == "customer001@amrod-demo.local",
            cancellationToken))
        {
            return;
        }

        var countries = new[]
        {
            ("ZA", "ZAR"),
            ("BW", "BWP"),
            ("NA", "NAD"),
            ("ZM", "ZMW"),
            ("ZW", "USD"),
            ("MZ", "MZN"),
            ("MW", "MWK"),
            ("TZ", "TZS")
        };

        var customers = new List<Customer>();

        for (var i = 1; i <= 100; i++)
        {
            var country = countries[(i - 1) % countries.Length];

            customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Name = $"Seed Customer {i:D3}",
                Email = $"customer{i:D3}@amrod-demo.local",
                CountryCode = country.Item1,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await db.Customers.AddRangeAsync(customers, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var statuses = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Paid,
            OrderStatus.Fulfilled,
            OrderStatus.Cancelled
        };

        var orders = new List<Order>();

        for (var i = 1; i <= 1000; i++)
        {
            var customer = customers[(i - 1) % customers.Count];

            var country = countries
                .First(c => c.Item1 == customer.CountryCode);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                CountryCode = customer.CountryCode,
                CurrencyCode = country.Item2,
                Status = statuses[(i - 1) % statuses.Length],
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                LineItems =
                [
                    new OrderLineItem
                    {
                        Id = Guid.NewGuid(),
                        ProductCode = $"SKU-{(i % 50) + 1:D3}",
                        Description = $"Seed Product {(i % 50) + 1}",
                        Quantity = (i % 5) + 1,
                        UnitPrice = 50m + (i % 20) * 10m
                    }
                ]
            };

            order.CalculateTotal();
            orders.Add(order);
        }

        await db.Orders.AddRangeAsync(orders, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}