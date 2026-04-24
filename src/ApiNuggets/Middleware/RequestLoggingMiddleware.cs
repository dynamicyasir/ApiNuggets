using System.Diagnostics;
using ApiNuggets.Models;
using ApiNuggets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiNuggets.Middleware;

/// <summary>
/// Captures method, path, status, duration, timestamp for each request and
/// forwards the entry to <see cref="IAnalyticsStore"/>.
/// </summary>
internal sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAnalyticsStore _analytics;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly ApiNuggetsOptions _options;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        IAnalyticsStore analytics,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<ApiNuggetsOptions> options)
    {
        _next = next;
        _analytics = analytics;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Don't record the dashboard's own traffic — it polls itself every
        // few seconds and would otherwise dominate the TopEndpoints list.
        var path = context.Request.Path.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(_options.Dashboard.Path) &&
            path.StartsWith(_options.Dashboard.Path, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var timestamp = DateTimeOffset.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;

            var entry = new RequestLogEntry(
                Timestamp: timestamp,
                Method: context.Request.Method,
                Path: context.Request.Path.Value ?? string.Empty,
                StatusCode: context.Response.StatusCode,
                DurationMs: elapsed,
                ClientIp: context.Connection.RemoteIpAddress?.ToString(),
                User: context.User?.Identity?.IsAuthenticated == true
                    ? context.User.Identity!.Name
                    : null);

            _analytics.Record(entry);

            var level = entry.StatusCode >= 500
                ? LogLevel.Error
                : entry.DurationMs > _options.Analytics.SlowRequestThresholdMs
                    ? LogLevel.Warning
                    : LogLevel.Information;

            _logger.Log(
                level,
                "HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms",
                entry.Method, entry.Path, entry.StatusCode, entry.DurationMs);
        }
    }
}
