using Amrod.OrderManagement.Api.Data;
using Amrod.OrderManagement.Api.DTOs;
using Amrod.OrderManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Amrod.OrderManagement.Api.Messaging;
using Microsoft.AspNetCore.Authorization;

namespace Amrod.OrderManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderManagementDbContext _db;
    private readonly IRabbitMqPublisher _rabbitMQPublisher;

    public OrdersController(
        OrderManagementDbContext db,
        IRabbitMqPublisher rabbitMqPublisher)
    {
        _db = db;
        _rabbitMQPublisher = rabbitMqPublisher;
    }

    [HttpPost]
    [Authorize(Policy = "Orders.Write")]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == request.CustomerId,
                cancellationToken);

        if (customer is null)
        {
            return BadRequest(new
            {
                message = "Customer does not exist."
            });
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.LineItems)
        {
            order.LineItems.Add(new OrderLineItem
            {
                Id = Guid.NewGuid(),
                ProductCode = item.ProductCode.Trim(),
                Description = item.Description.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        order.CalculateTotal();

        _db.Orders.Add(order);

        await _db.SaveChangesAsync(cancellationToken);

        // to get correlation ID
        var correlationId =
            HttpContext.Items["X-Correlation-ID"]?.ToString()
            ?? HttpContext.TraceIdentifier;

        await _rabbitMQPublisher.PublishOrderCreatedAsync(
            new OrderCreatedEvent(
                order.Id,
                order.CustomerId,
                order.TotalAmount,
                order.CountryCode,
                order.CurrencyCode,
                order.CreatedAt),
        correlationId,
        cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            ToResponse(order));
    }

    [HttpGet]
    [Authorize(Policy = "Orders.Read")]
    public async Task<ActionResult<object>> GetAll(
        [FromQuery] Guid? customerId,
        [FromQuery] OrderStatus? status,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] bool descending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.LineItems)
            .AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        query = sortBy?.ToLowerInvariant() switch
        {
            "totalamount" => descending
                ? query.OrderByDescending(o => o.TotalAmount)
                : query.OrderBy(o => o.TotalAmount),

            "status" => descending
                ? query.OrderByDescending(o => o.Status)
                : query.OrderBy(o => o.Status),

            _ => descending
                ? query.OrderByDescending(o => o.CreatedAt)
                : query.OrderBy(o => o.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderResponse(
                o.Id,
                o.CustomerId,
                o.CountryCode,
                o.CurrencyCode,
                o.Status,
                o.TotalAmount,
                o.CreatedAt,
                o.UpdatedAt,
                o.LineItems
                    .Select(item => new OrderLineItemResponse(
                        item.Id,
                        item.ProductCode,
                        item.Description,
                        item.Quantity,
                        item.UnitPrice,
                        item.Quantity * item.UnitPrice))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items = orders,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Orders.Read")]
    public async Task<ActionResult<OrderResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(
                o => o.Id == id,
                cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var etag = $"\"{Convert.ToBase64String(order.RowVersion)}\"";

        Response.Headers.ETag = etag;

        if (Request.Headers.IfNoneMatch == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(ToResponse(order));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "Orders.Write")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(
        Guid id,
        UpdateOrderStatusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new
            {
                message = "Idempotency-Key header is required."
            });
        }

        if (idempotencyKey.Length > 100)
        {
            return BadRequest(new
            {
                message = "Idempotency-Key must not exceed 100 characters."
            });
        }

        var order = await _db.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        // Repeat request with the same key is treated as idempotent.
        if (order.LastStatusIdempotencyKey == idempotencyKey)
        {
            return Ok(ToResponse(order));
        }

        if (!IsValidStatusTransition(order.Status, request.Status))
        {
            return BadRequest(new
            {
                message = $"Invalid status transition from {order.Status} to {request.Status}."
            });
        }

        order.Status = request.Status;
        order.LastStatusIdempotencyKey = idempotencyKey;
        order.StatusUpdatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "The order was modified by another request. Please retry."
            });
        }

        return Ok(ToResponse(order));
    }

    private static bool IsValidStatusTransition(
        OrderStatus current,
        OrderStatus next)
    {
        return current switch
        {
            OrderStatus.Pending =>
                next is OrderStatus.Paid or OrderStatus.Cancelled,

            OrderStatus.Paid =>
                next is OrderStatus.Fulfilled or OrderStatus.Cancelled,

            OrderStatus.Fulfilled => false,

            OrderStatus.Cancelled => false,

            _ => false
        };
    }

    private static OrderResponse ToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.CustomerId,
            order.CountryCode,
            order.CurrencyCode,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,
            order.UpdatedAt,
            order.LineItems
                .Select(item => new OrderLineItemResponse(
                    item.Id,
                    item.ProductCode,
                    item.Description,
                    item.Quantity,
                    item.UnitPrice,
                    item.Quantity * item.UnitPrice))
                .ToList());
    }
}