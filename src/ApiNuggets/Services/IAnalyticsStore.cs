using ApiNuggets.Models;

namespace ApiNuggets.Services;

/// <summary>In-memory store for captured request logs and dashboard rollups.</summary>
public interface IAnalyticsStore
{
    /// <summary>Records a completed request. No-op if analytics is disabled.</summary>
    void Record(RequestLogEntry entry);

    /// <summary>Returns an aggregated snapshot suitable for the dashboard endpoint.</summary>
    AnalyticsSnapshot GetSnapshot(int topEndpointCount = 5);

    /// <summary>The most recent entries, newest first.</summary>
    IReadOnlyCollection<RequestLogEntry> GetRecent(int max = 100);

    /// <summary>Clears the in-memory buffer. Primarily useful for tests.</summary>
    void Clear();
}
