using Amrod.OrderManagement.Api.Data;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Amrod.OrderManagement.Api.Validators;
using Amrod.OrderManagement.Api.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Amrod.OrderManagement.Api.Middleware;
using Amrod.OrderManagement.Api.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddDbContext<OrderManagementDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IRabbitMqPublisher, RabbitMqPublisher>();

// Basic in-memory request metrics.
// Singleton is used so metrics are accumulated
// across requests for the lifetime of the API process.
builder.Services.AddSingleton<RequestMetrics>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is not configured.");

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? "Amrod.OrderManagement";

var jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? "Amrod.OrderManagement.Client";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "Orders.Read",
        policy =>
            policy.RequireClaim(
                "permission",
                "Orders.Read",
                "Orders.Write",
                "Orders.Admin"));

    options.AddPolicy(
        "Orders.Write",
        policy =>
            policy.RequireClaim(
                "permission",
                "Orders.Write",
                "Orders.Admin"));

    options.AddPolicy(
        "Orders.Admin",
        policy =>
            policy.RequireClaim(
                "permission",
                "Orders.Admin"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

/*
 * Run migrations and seed demo data for normal
 * application environments.
 *
 * Integration tests set the environment to
 * "Testing", so WebApplicationFactory does not
 * migrate, seed, or modify the normal database.
 */
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope =
        app.Services.CreateScope();

    var db =
        scope.ServiceProvider
            .GetRequiredService<OrderManagementDbContext>();

    await db.Database.MigrateAsync();

    await DatabaseSeeder.SeedAsync(db);
}

/*
 * Correlation ID is registered before request
 * metrics so request logs can be associated with
 * the same request identifier.
 */
app.UseMiddleware<CorrelationIdMiddleware>();

/*
 * Records request count, response status and
 * request duration.
 */
app.UseMiddleware<RequestMetricsMiddleware>();

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet(
    "/healthz",
    () =>
        Results.Ok(
            new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow
            }));

app.MapGet(
    "/readiness",
    async (
        OrderManagementDbContext db,
        CancellationToken cancellationToken) =>
    {
        var canConnect =
            await db.Database.CanConnectAsync(
                cancellationToken);

        return canConnect
            ? Results.Ok(
                new
                {
                    status = "Ready",
                    database = "Connected",
                    timestamp = DateTime.UtcNow
                })
            : Results.StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable);
    });

/*
 * Basic assessment metrics endpoint.
 *
 * For production I would normally expose
 * OpenTelemetry/Prometheus metrics instead of
 * maintaining process-local counters.
 */
app.MapGet(
    "/metrics",
    (RequestMetrics metrics) =>
        Results.Ok(metrics.GetSnapshot()));

app.Run();

/*
 * Makes Program visible to
 * WebApplicationFactory<Program>.
 */
public partial class Program
{
}