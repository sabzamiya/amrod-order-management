# Amrod Order Management System – Technical Assessment Answers

## General Questions

### 1. .NET/C#: Explain async/await best practices in ASP.NET Core for I/O-bound work. When would you use Task.Run in a web API?

In ASP.NET Core, I use `async/await` mainly when working with I/O operations such as database queries, saving data, calling external APIs or working with messaging services.

For example, in this project I use asynchronous EF Core methods such as:

```csharp
var order = await _db.Orders
    .AsNoTracking()
    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
```

The main reason is to avoid blocking a thread while waiting for the database or another external service to respond. This allows the API to handle more requests efficiently.

I try to keep the code asynchronous throughout the request flow. I avoid using `.Result` or `.Wait()` because they block threads unnecessarily. Where possible, I also pass a `CancellationToken` to asynchronous operations.

I would not normally use `Task.Run` for I/O operations in a web API because libraries such as EF Core already provide asynchronous methods.

I would only consider `Task.Run` for CPU-intensive work that cannot be performed asynchronously. If the work is long-running, I would rather move it to a background process instead of keeping the HTTP request waiting.

For example, in this project I use RabbitMQ and a Worker for downstream order processing. The API creates the order and publishes an `OrderCreated` event, while the Worker processes it separately.

---

### 2. ASP.NET Core: Minimal APIs vs controller-based APIs – pros/cons and when would you prefer each?

I would choose between Minimal APIs and controllers depending on the size and complexity of the application.

Minimal APIs are useful for small services where there are only a few endpoints. They require less boilerplate and are quick to set up.

For example, simple endpoints such as:

```text
/healthz
/readiness
```

can work well as Minimal APIs because they do not require much business logic.

Controller-based APIs provide more structure. I prefer controllers when an application has multiple related endpoints, authorization policies, validation and more business logic.

For this project, I used controllers for Customers and Orders because it keeps the endpoints organised and makes the API easier to maintain.

For example:

```text
CustomersController
OrdersController
AuthController
```

Controllers also make things such as attribute routing and policy-based authorization straightforward:

```csharp
[Authorize(Policy = "Orders.Read")]
```

For a small microservice I would consider Minimal APIs. For a larger business API, I would normally prefer controllers because the structure is easier to maintain as the application grows.

---

### 3. EF Core: Tracking vs AsNoTracking(). How would you implement optimistic concurrency safely?

By default, EF Core tracks entities that it retrieves from the database. Tracking is useful when I intend to modify an entity and call `SaveChangesAsync()` because EF Core needs to know what changed.

For read-only queries I use `AsNoTracking()`.

For example:

```csharp
var orders = await _db.Orders
    .AsNoTracking()
    .ToListAsync();
```

This avoids unnecessary change-tracking overhead when I know the returned entities will not be updated.

I used this approach on the read endpoints in this project.

For optimistic concurrency, I use a SQL Server `rowversion` column on the Order entity:

```csharp
public byte[] RowVersion { get; set; } = Array.Empty<byte>();
```

and configure it in EF Core:

```csharp
builder.Property(o => o.RowVersion)
    .IsRowVersion();
```

When EF Core updates the order, it checks the original row version.

If another request changes the same order before my update completes, the row version will no longer match and EF Core throws:

```csharp
DbUpdateConcurrencyException
```

I handle that exception and return a `409 Conflict`.

This prevents one update from silently overwriting another update.

---

### 4. Validation: Where do you enforce business rules (DTO vs domain vs DB)? Would you use FluentValidation or manual validation, and why?

I normally validate at more than one level because different types of validation belong in different places.

At the request/DTO level, I validate things such as:

- Required fields
- Email format
- Country code
- Currency code
- Quantity greater than zero
- Unit price not being negative

In this project I used FluentValidation because it keeps validation rules separate from the controllers and makes them easier to read and test.

For example, the order validator checks whether the selected currency is valid for the SADC country.

Business rules should also be enforced in the application/domain logic.

For example, the server calculates:

```text
TotalAmount = Sum(Quantity × UnitPrice)
```

I would never trust a total sent by the frontend because the client can be changed or bypassed.

The order status transitions are also business rules. For example:

```text
Pending -> Paid
Pending -> Cancelled
Paid -> Fulfilled
Paid -> Cancelled
```

At database level, I use constraints for things such as foreign keys, unique values, maximum lengths and decimal precision.

This gives the application multiple levels of protection instead of relying only on frontend validation.

---

### 5. Exception Handling: Implementing global handlers and Problem Details (RFC7807); what should error contracts look like?

For a production API, I prefer having global exception handling rather than putting large `try/catch` blocks in every controller.

ASP.NET Core supports `ProblemDetails`, which provides a standard structure for API errors.

For example:

```json
{
  "title": "Concurrency conflict",
  "status": 409,
  "detail": "The order was updated by another process.",
  "traceId": "abc-123"
}
```

I would normally map errors to the appropriate HTTP status code:

- `400` for invalid input
- `401` when authentication is required
- `403` when the user is authenticated but does not have permission
- `404` when a resource cannot be found
- `409` for concurrency conflicts
- `500` for unexpected server errors

The error response should be consistent so that the frontend knows what structure to expect.

I would also include a correlation or trace ID so that an error returned to the client can be matched to application logs.

I would not return internal stack traces, database connection information or other sensitive technical information to the client.

---

### 6. API Design: REST search endpoints – query params, pagination metadata, sorting; when is GraphQL preferable; N+1 pitfalls.

For REST list endpoints, I normally use query parameters for filtering, searching, sorting and pagination.

For example:

```http
GET /api/orders?customerId=123&status=Paid&page=1&pageSize=20&sortBy=createdAt&descending=true
```

The API should validate the pagination values and enforce a maximum page size.

In this project, the maximum `pageSize` is 100.

A paginated response should also return metadata such as:

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 250,
  "items": []
}
```

This gives the frontend enough information to build pagination controls.

For sorting, I only allow known fields instead of allowing arbitrary database column names from the request.

GraphQL can be useful when clients need very flexible queries or different clients need different combinations of related data. The client can request only the fields it requires.

For this Order Management system, REST is enough because the resources and operations are straightforward.

One problem I always consider when retrieving related data is the N+1 query problem.

For example, retrieving 100 orders and then making another database query for the line items of each order could result in 101 queries.

I avoid that by loading or projecting the required related data in the original query, for example using `Include()` when appropriate.

---

### 7. Security (Entra): Validating JWT via Microsoft.Identity.Web, mapping roles/claims, token lifetimes, and policy-based authorization.

For a production application using Microsoft Entra ID, I would configure the API to validate access tokens issued by the correct Entra tenant.

The important things to validate include:

- Token signature
- Issuer
- Audience
- Expiration
- Required roles, scopes or claims

I prefer policy-based authorization instead of manually checking permissions inside each controller action.

For this project the policies are:

```text
Orders.Read
Orders.Write
Orders.Admin
```

For example:

```csharp
[Authorize(Policy = "Orders.Read")]
```

This makes the security requirements clear at endpoint level.

Because this is an assessment and an Entra tenant was not required, I implemented a mock JWT token issuer while still configuring normal JWT validation and authorization policies.

For production, the mock issuer would be removed and replaced by Microsoft Entra ID configuration.

Access tokens should also have limited lifetimes. I would rely on the identity provider's supported authentication/token renewal flow instead of issuing permanent tokens.

Secrets such as signing keys must also be kept outside source control.

---

### 8. Messaging (RabbitMQ): Exchange types, queue durability, at-least-once delivery, and designing idempotency.

RabbitMQ supports different exchange types depending on how messages need to be routed.

A `direct` exchange routes a message based on an exact routing key. This is what I used in this project.

A `fanout` exchange sends the message to all queues bound to that exchange.

A `topic` exchange supports routing patterns and wildcards, which is useful when there are multiple related event types.

RabbitMQ also supports header-based routing.

For important business messages, I configure durable queues and persistent messages so that they can survive a broker restart.

In this project, the API publishes an `OrderCreated` event and the Worker consumes it.

I assume messages can be delivered more than once. This is important because a consumer could successfully process a message and then fail before acknowledging it.

Because of that, the consumer must be idempotent.

In my Worker, if the order is already `Fulfilled`, receiving the same event again does not repeat the transition.

I also implemented retry handling.

If processing fails, the message is sent to a retry queue. After a delay it is returned for another processing attempt.

After the maximum retry count is reached, the message is moved to a dead-letter queue where it can be investigated.

One improvement I would make for production is the Transactional Outbox Pattern.

At the moment, the API saves the Order and then publishes the RabbitMQ message. There is a small possibility that the database operation succeeds but RabbitMQ publication fails.

With an Outbox Pattern, I would save both the Order and an Outbox record in the same SQL transaction. A background publisher would then publish the event from the Outbox.

---

### 9. Frontend (React + TypeScript): State management choices and how do you keep API contracts type-safe?

My choice of state management depends on the size and requirements of the frontend.

I would use React Context for small amounts of shared state such as authentication information, theme or user preferences.

For a larger application with complex client-side state, Redux Toolkit can provide more structured and predictable state management.

For server-side data, tools such as RTK Query or SWR are useful because they provide caching, loading states, refetching and cache invalidation.

For this assessment, the frontend is relatively small, so I used a custom API layer rather than adding Redux just for the sake of using it.

I created TypeScript interfaces for the API models.

For example:

```typescript
export interface Order {
  id: string;
  customerId: string;
  countryCode: string;
  currencyCode: string;
  status: OrderStatus;
  totalAmount: number;
  createdAt: string;
  updatedAt?: string;
  lineItems: OrderLineItem[];
}
```

This gives compile-time type checking when the API data is used in React components.

For a larger production application, I would consider generating the TypeScript client and types from the OpenAPI specification. This would reduce the possibility of the frontend and backend contracts becoming different over time.

---

### 10. Testing/CI: Your test pyramid for this stack; what belongs in unit vs integration vs E2E; CI gates you would add.

I would use a test pyramid where most tests are fast unit tests, followed by integration tests, with a smaller number of end-to-end tests.

Unit tests should focus on individual business rules and functions.

For this project, examples include:

- Order total calculation
- SADC country/currency validation
- Request validation

Integration tests should verify that different parts of the application work together.

I used `WebApplicationFactory` for API integration testing.

My integration tests cover areas such as:

- Authentication
- Authorization
- Customer creation
- Order creation
- Order retrieval
- RabbitMQ event publication through a test publisher
- Status transitions
- Idempotency
- Invalid status transitions
- Health endpoint

For frontend component testing I used React Testing Library and Vitest to test the customer and order screens.

E2E tests should cover a few important user journeys rather than every possible scenario.

For example:

```text
Create Customer
    ->
Create Order
    ->
Update / Process Order
```

Playwright would be a good option for this.

In CI, I would include:

- Restore dependencies
- Build
- Lint/static analysis
- Unit tests
- Integration tests
- Frontend tests
- Production frontend build
- Database migration validation

For production I would also add coverage thresholds, dependency vulnerability scanning, secret scanning and container image scanning.

---

### 11. Performance/Scalability/Observability: Handling write spikes, indexing, caching, correlation IDs and tracing, SLOs.

For observability, I also implemented a simple /metrics endpoint that tracks API request count, average request duration and response status-code counts. I use correlation IDs so that requests can also be followed from the API into RabbitMQ and the Worker.

In a larger production environment, I would extend this with metrics such as queue depth, retry count, dead-letter messages, database query duration and Worker processing time. I would then use these metrics when defining and monitoring SLOs for availability and response times.

---

# SQL Section

### 12. Pagination Query

To return a customer's Orders with pagination, I would use `OFFSET` and `FETCH`.

```sql
SELECT
    o.Id,
    o.CustomerId,
    o.Status,
    o.CreatedAt,
    o.TotalAmount
FROM dbo.Orders AS o
WHERE o.CustomerId = @CustomerId
ORDER BY o.CreatedAt DESC
OFFSET @Offset ROWS
FETCH NEXT @PageSize ROWS ONLY;
```

Then I would run a separate count query:

```sql
SELECT COUNT(*) AS TotalCount
FROM dbo.Orders
WHERE CustomerId = @CustomerId;
```

The parameters would be passed from the application:

```text
@CustomerId
@Offset
@PageSize
```

For example:

```text
Offset = (Page - 1) * PageSize
```

I would parameterise these values rather than building them directly into the SQL string.

---

### 13. Top Spenders – Last 90 Days

I would use a `LEFT JOIN` because the requirement says customers with no Orders must still be included.

```sql
SELECT TOP (10)
    c.Id AS CustomerId,
    c.Name,
    c.Email,
    COALESCE(SUM(o.TotalAmount), 0) AS TotalSpend
FROM dbo.Customers AS c
LEFT JOIN dbo.Orders AS o
    ON o.CustomerId = c.Id
    AND o.CreatedAt >= DATEADD(DAY, -90, SYSUTCDATETIME())
GROUP BY
    c.Id,
    c.Name,
    c.Email
ORDER BY
    TotalSpend DESC,
    c.Id;
```

`COALESCE` changes a `NULL` total into `0`.

I put the date condition in the `JOIN` rather than the `WHERE` clause because putting it in the `WHERE` clause could remove customers that have no matching Orders.

---

### 14. Indexing

For the common Order query pattern, I would create:

```sql
CREATE INDEX IX_Orders_CustomerId_Status_CreatedAt
ON dbo.Orders
(
    CustomerId,
    Status,
    CreatedAt
);
```

This is also the index I created through EF Core in the project.

It helps SQL Server find Orders for a particular customer and status without scanning the whole Orders table.

If a listing query frequently needs other fields such as `TotalAmount` and `CurrencyCode`, I could consider a covering index:

```sql
CREATE INDEX IX_Orders_CustomerId_Status_CreatedAt_Covering
ON dbo.Orders
(
    CustomerId,
    Status,
    CreatedAt DESC
)
INCLUDE
(
    TotalAmount,
    CurrencyCode
);
```

The benefit is that SQL Server may be able to satisfy the query directly from the index without going back to the main table/index for the other columns.

The trade-off is that additional indexes consume storage and make inserts and updates slightly more expensive because SQL Server must maintain those indexes.

Because of that, I would add covering indexes based on actual query performance rather than adding many indexes unnecessarily.

---

### 15. Execution Plan & Key Lookups

If a query is slow, I would inspect its actual execution plan in SQL Server.

For example:

```sql
SELECT
    o.Id,
    o.CreatedAt,
    o.TotalAmount,
    li.ProductCode,
    li.Quantity,
    li.UnitPrice
FROM dbo.Orders AS o
INNER JOIN dbo.OrderLineItems AS li
    ON li.OrderId = o.Id
WHERE
    o.CustomerId = @CustomerId
    AND o.Status = @Status;
```

If the execution plan shows something like:

```text
Index Seek
   ->
Key Lookup
```

it means SQL Server used a nonclustered index to find the records but then had to go back to the clustered index to retrieve columns that were not available in the first index.

If the Key Lookup is expensive and the query is frequently used, I could include the required columns in the index.

For example:

```sql
CREATE INDEX IX_Orders_CustomerId_Status_CreatedAt_Covering
ON dbo.Orders
(
    CustomerId,
    Status,
    CreatedAt
)
INCLUDE
(
    TotalAmount,
    CurrencyCode
);
```

I would also make sure `OrderLineItems.OrderId` has an appropriate index for the join.

I would make the final indexing decision based on the actual execution plan and how frequently the query runs.

---

### 16. Optimistic Concurrency

For optimistic concurrency on Orders, I use SQL Server `rowversion`.

At SQL level it can be added as:

```sql
ALTER TABLE dbo.Orders
ADD RowVersion ROWVERSION NOT NULL;
```

In the EF Core entity:

```csharp
public byte[] RowVersion { get; set; } = Array.Empty<byte>();
```

Then configure it:

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.RowVersion)
    .IsRowVersion();
```

When saving an update:

```csharp
try
{
    order.Status = OrderStatus.Paid;
    order.UpdatedAt = DateTime.UtcNow;

    await _db.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    return Conflict(new
    {
        error = "Concurrency conflict",
        message = "The order was updated by another process."
    });
}
```

The important part is that EF Core checks whether the row version is still the same as when the Order was originally read.

If another process changed the Order first, EF Core detects that and throws `DbUpdateConcurrencyException`.

This prevents one user or process from unknowingly overwriting another update.

---

### 17. Deadlocks

A deadlock can happen when two transactions hold locks that the other transaction needs.

For example:

```text
Transaction A locks Order 1
Transaction B locks Order 2

Transaction A then needs Order 2
Transaction B then needs Order 1
```

Neither can continue, so SQL Server detects the deadlock and chooses one transaction as the victim.

Some ways I would reduce deadlocks are:

- Keep transactions as short as possible.
- Access records in a consistent order.
- Make sure queries have suitable indexes.
- Avoid unnecessary work while a transaction is open.
- Retry an operation if it is selected as a deadlock victim.

For workloads with a lot of reader/writer blocking, I would also consider `READ_COMMITTED_SNAPSHOT`.

For example:

```sql
ALTER DATABASE AmrodOrderManagement
SET READ_COMMITTED_SNAPSHOT ON;
```

I would be careful with locking hints because they can solve one problem while creating another if they are used without understanding the workload.

---

### 18. Window Functions

To calculate a running total for each customer, I would use `SUM()` with `OVER()`:

```sql
SELECT
    o.CustomerId,
    o.Id AS OrderId,
    o.CreatedAt,
    o.TotalAmount,
    SUM(o.TotalAmount) OVER
    (
        PARTITION BY o.CustomerId
        ORDER BY o.CreatedAt, o.Id
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS RunningTotal
FROM dbo.Orders AS o
ORDER BY
    o.CustomerId,
    o.CreatedAt,
    o.Id;
```

`PARTITION BY CustomerId` means each customer gets their own running total.

`ORDER BY CreatedAt` controls the order in which each Order is added to that running total.

I also use the Order ID as a second sorting value so that the result remains predictable if two Orders have the same `CreatedAt`.

---

### 19. Partitioning Strategy

If the Orders table grew to millions of records, I would consider partitioning it by `CreatedAt`, for example monthly partitions.

Conceptually:

```text
January 2026
February 2026
March 2026
...
```

In SQL Server, this can be implemented using a partition function and partition scheme.

The benefit is that date-based queries may only need to access the relevant partitions instead of scanning the entire table.

It can also make archiving old Orders easier.

For example, if the business keeps recent Orders in the main operational database and archives older data, an old monthly partition can be switched to an archive table.

Partition switching is useful because SQL Server can move the partition using metadata operations instead of copying millions of records row by row.

I would still keep appropriate indexes because partitioning does not replace indexing.

I would also only introduce partitioning once the size and workload justify the additional database complexity.

---

### 20. Stored Procedure – Transaction Report

I would create the stored procedure with the required date, status and customer filters.

One thing I would be careful about is the end date. Because `CreatedAt` contains a time, I prefer using:

```text
CreatedAt >= StartDate
AND CreatedAt < day after EndDate
```

instead of trying to compare against `23:59:59`.

```sql
CREATE OR ALTER PROCEDURE dbo.sp_GetTransactionReport
    @StartDate DATE,
    @EndDate DATE,
    @Status NVARCHAR(20) = NULL,
    @CustomerId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @EndDate < @StartDate
    BEGIN
        THROW 50001, 'EndDate cannot be earlier than StartDate.', 1;
    END;

    DECLARE @EndExclusive DATETIME2 =
        DATEADD(DAY, 1, CAST(@EndDate AS DATETIME2));

    DECLARE @StatusValue INT =
        CASE @Status
            WHEN 'Pending' THEN 1
            WHEN 'Paid' THEN 2
            WHEN 'Fulfilled' THEN 3
            WHEN 'Cancelled' THEN 4
            ELSE NULL
        END;

    IF @Status IS NOT NULL AND @StatusValue IS NULL
    BEGIN
        THROW 50002, 'Invalid order status.', 1;
    END;

    -- Result Set 1: Detailed rows
    SELECT
        c.Name AS CustomerName,
        c.Email,
        c.CountryCode,
        o.Id AS OrderId,
        o.Status,
        o.CreatedAt,
        o.CurrencyCode,
        o.TotalAmount,
        li.ProductCode AS ProductSku,
        li.Quantity,
        li.UnitPrice
    FROM dbo.Orders AS o
    INNER JOIN dbo.Customers AS c
        ON c.Id = o.CustomerId
    INNER JOIN dbo.OrderLineItems AS li
        ON li.OrderId = o.Id
    WHERE
        o.CreatedAt >= @StartDate
        AND o.CreatedAt < @EndExclusive
        AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
        AND (@StatusValue IS NULL OR o.Status = @StatusValue)
    ORDER BY
        o.CreatedAt DESC,
        o.Id;

    -- Result Set 2: Summary
    SELECT
        COUNT(*) AS TotalOrders,
        COALESCE(SUM(o.TotalAmount), 0) AS GrandTotalAmount
    FROM dbo.Orders AS o
    WHERE
        o.CreatedAt >= @StartDate
        AND o.CreatedAt < @EndExclusive
        AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
        AND (@StatusValue IS NULL OR o.Status = @StatusValue);
END;
GO
```

The summary query works directly against `Orders` rather than joining to `OrderLineItems`.

This is important because if one Order has three line items and I summed `TotalAmount` after joining the line items, that Order's total could be counted three times.

In this project, `OrderStatus` is stored as an integer, so I map the status text to its enum value before filtering.

For performance, I already have the index:

```text
IX_Orders_CustomerId_Status_CreatedAt
```

If this report became heavily used, I would inspect its execution plan and consider whether including `CurrencyCode` and `TotalAmount` in a covering index would improve performance.