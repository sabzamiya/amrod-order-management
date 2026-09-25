using Amrod.OrderManagement.Api.Data;
using Amrod.OrderManagement.Api.DTOs;
using Amrod.OrderManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Amrod.OrderManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly OrderManagementDbContext _db;

    public CustomersController(OrderManagementDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Authorize(Policy = "Orders.Write")]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        if (await _db.Customers.AnyAsync(
                c => c.Email == customer.Email,
                cancellationToken))
        {
            return Conflict(new
            {
                message = "A customer with this email already exists."
            });
        }

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = customer.Id },
            ToResponse(customer));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Orders.Read")]
    public async Task<ActionResult<CustomerResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(customer));
    }

    [HttpGet]
    [Authorize(Policy = "Orders.Read")]
    public async Task<ActionResult<object>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(c =>
                c.Name.Contains(search) ||
                c.Email.Contains(search) ||
                c.CountryCode.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerResponse(
                c.Id,
                c.Name,
                c.Email,
                c.CountryCode,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items = customers,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private static CustomerResponse ToResponse(Customer customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.CountryCode,
            customer.CreatedAt);
}