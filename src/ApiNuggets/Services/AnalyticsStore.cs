using System.Collections.Concurrent;
using ApiNuggets.Models;
using Microsoft.Extensions.Options;

namespace ApiNuggets.Services;

/// <inheritdoc />
internal sealed class AnalyticsStore : IAnalyticsStore
{
    private readonly ApiNuggetsOptions _options;
    private readonly ConcurrentQueue<RequestLogEntry> _entries = new();
    private int _count;

    public AnalyticsStore(IOptions<ApiNuggetsOptions> options)
    {
        _options = options.Value;
    }

    public void Record(RequestLogEntry entry)
    {
        if (!_options.Analytics.Enable)
        {
            return;
        }

        _entries.Enqueue(entry);
        var newCount = Interlocked.Increment(ref _count);

        // Keep the ring at MaxLogEntries. Dequeue until we're back under the cap.
        var max = Math.Max(1, _options.Analytics.MaxLogEntries);
        while (newCount > max && _entries.TryDequeue(out _))
        {
            newCount = Interlocked.Decrement(ref _count);
        }
    }

    public AnalyticsSnapshot GetSnapshot(int topEndpointCount = 5)
    {
        // ToArray on a ConcurrentQueue is a consistent snapshot.
        var entries = _entries.ToArray();

        if (entries.Length == 0)
        {
            return new AnalyticsSnapshot
            {
                TotalRequests = 0,
                AvgResponseTime = 0,
                SlowRequests = 0,
                ErrorCount = 0,
                TopEndpoints = Array.Empty<EndpointStat>(),
            };
        }

        var threshold = _options.Analytics.SlowRequestThresholdMs;
        long totalDuration = 0;
        var slow = 0;
        var errors = 0;

        foreach (var e in entries)
        {
            totalDuration += e.DurationMs;
            if (e.DurationMs > threshold) slow++;
            if (e.StatusCode >= 500) errors++;
        }

        var topEndpoints = entries
            .GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EndpointStat(
                Path: g.Key,
                RequestCount: g.Count(),
                AvgResponseTime: Math.Round(g.Average(x => (double)x.DurationMs), 2),
                ErrorCount: g.Count(x => x.StatusCode >= 500)))
            .OrderByDescending(s => s.RequestCount)
            .Take(Math.Max(1, topEndpointCount))
            .ToArray();

        return new AnalyticsSnapshot
        {
            TotalRequests = entries.Length,
            AvgResponseTime = Math.Round((double)totalDuration / entries.Length, 2),
            SlowRequests = slow,
            ErrorCount = errors,
            TopEndpoints = topEndpoints,
        };
    }

    public IReadOnlyCollection<RequestLogEntry> GetRecent(int max = 100)
    {
        var entries = _entries.ToArray();
        if (entries.Length <= max)
        {
            Array.Reverse(entries);
            return entries;
        }

        var slice = new RequestLogEntry[max];
        Array.Copy(entries, entries.Length - max, slice, 0, max);
        Array.Reverse(slice);
        return slice;
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _count, 0);
    }
}
