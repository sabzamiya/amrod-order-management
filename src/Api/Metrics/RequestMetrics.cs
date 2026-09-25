using System.Collections.Concurrent;

namespace Amrod.OrderManagement.Api.Metrics;

public class RequestMetrics
{
    private long _totalRequests;
    private long _totalDurationMilliseconds;

    private readonly ConcurrentDictionary<int, long>
        _statusCodeCounts = new();

    public void RecordRequest(
        int statusCode,
        long durationMilliseconds)
    {
        Interlocked.Increment(ref _totalRequests);

        Interlocked.Add(
            ref _totalDurationMilliseconds,
            durationMilliseconds);

        _statusCodeCounts.AddOrUpdate(
            statusCode,
            1,
            (_, current) => current + 1);
    }

    public object GetSnapshot()
    {
        var totalRequests =
            Interlocked.Read(ref _totalRequests);

        var totalDuration =
            Interlocked.Read(
                ref _totalDurationMilliseconds);

        var averageDuration =
            totalRequests == 0
                ? 0
                : Math.Round(
                    (double)totalDuration /
                    totalRequests,
                    2);

        return new
        {
            totalRequests,
            averageDurationMilliseconds =
                averageDuration,
            statusCodes =
                _statusCodeCounts
                    .OrderBy(x => x.Key)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value)
        };
    }
}