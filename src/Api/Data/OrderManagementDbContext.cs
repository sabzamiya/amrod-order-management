using Amrod.OrderManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Amrod.OrderManagement.Api.Data;

public class OrderManagementDbContext : DbContext
{
    public OrderManagementDbContext(
        DbContextOptions<OrderManagementDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLineItem> OrderLineItems => Set<OrderLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(c => c.Email)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(c => c.CountryCode)
                .HasMaxLength(2)
                .IsRequired();

            entity.HasIndex(c => c.Email)
                .IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(o => o.CountryCode)
                .HasMaxLength(2)
                .IsRequired();

            entity.Property(o => o.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(o => o.Status)
                .HasConversion<int>();

            entity.Property(o => o.LastStatusIdempotencyKey)
                .HasMaxLength(100);

            entity.HasIndex(o => o.LastStatusIdempotencyKey);

            entity.Property(o => o.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(o => o.LineItems)
                .WithOne(li => li.Order)
                .HasForeignKey(li => li.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => new
            {
                o.CustomerId,
                o.Status,
                o.CreatedAt
            })
            .HasDatabaseName(
                "IX_Orders_CustomerId_Status_CreatedAt");
        });

        modelBuilder.Entity<OrderLineItem>(entity =>
        {
            entity.HasKey(li => li.Id);

            entity.Property(li => li.ProductCode)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(li => li.Description)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(li => li.UnitPrice)
                .HasPrecision(18, 2);

            entity.Property(li => li.Quantity)
                .IsRequired();
        });
    }
}