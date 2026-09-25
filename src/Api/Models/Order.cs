namespace Amrod.OrderManagement.Api.Models;

public class Order
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public Customer Customer { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? LastStatusIdempotencyKey { get; set; }

    public DateTime? StatusUpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<OrderLineItem> LineItems { get; set; } = new List<OrderLineItem>();

    public void CalculateTotal()
    {
        TotalAmount = LineItems.Sum(
            item => item.Quantity * item.UnitPrice);
    }
}