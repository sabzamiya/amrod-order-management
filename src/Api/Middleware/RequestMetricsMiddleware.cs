using System.Diagnostics;
using Amrod.OrderManagement.Api.Metrics;

namespace Amrod.OrderManagement.Api.Middleware;

public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public RequestMetricsMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        RequestMetrics metrics)
    {
        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            metrics.RecordRequest(
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}