namespace ApiNuggets.Models;

/// <summary>Aggregated snapshot served by the dashboard endpoint.</summary>
public sealed class AnalyticsSnapshot
{
    public int TotalRequests { get; init; }
    public double AvgResponseTime { get; init; }
    public int SlowRequests { get; init; }
    public int ErrorCount { get; init; }
    public IReadOnlyList<EndpointStat> TopEndpoints { get; init; } = Array.Empty<EndpointStat>();
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Per-endpoint rollup for the dashboard.</summary>
public sealed record EndpointStat(
    string Path,
    int RequestCount,
    double AvgResponseTime,
    int ErrorCount
);
