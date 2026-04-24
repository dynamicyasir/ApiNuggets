namespace ApiNuggets.Models;

/// <summary>A single request captured by the logging + analytics pipeline.</summary>
public sealed record RequestLogEntry(
    DateTimeOffset Timestamp,
    string Method,
    string Path,
    int StatusCode,
    long DurationMs,
    string? ClientIp,
    string? User
);
