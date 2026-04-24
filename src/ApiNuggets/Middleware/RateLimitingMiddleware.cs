using System.Collections.Concurrent;
using System.Text.Json;
using ApiNuggets.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ApiNuggets.Middleware;

/// <summary>
/// Basic per-IP fixed-window rate limiter. Keeps a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// of counters keyed by client IP and resets each minute.
/// </summary>
internal sealed class RateLimitingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ApiNuggetsOptions _options;
    private readonly ConcurrentDictionary<string, Counter> _counters = new(StringComparer.Ordinal);

    public RateLimitingMiddleware(RequestDelegate next, IOptions<ApiNuggetsOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.RateLimiting.Enable || IsBypassed(context))
        {
            await _next(context);
            return;
        }

        var ip = GetClientIp(context);
        var now = DateTimeOffset.UtcNow;
        var limit = Math.Max(1, _options.RateLimiting.RequestsPerMinute);

        var counter = _counters.GetOrAdd(ip, _ => new Counter(now));

        int current;
        DateTimeOffset windowStart;

        lock (counter)
        {
            if (now - counter.WindowStart >= TimeSpan.FromMinutes(1))
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            counter.Count++;
            current = counter.Count;
            windowStart = counter.WindowStart;
        }

        var remaining = Math.Max(0, limit - current);
        var resetIn = (int)Math.Max(0, (windowStart.AddMinutes(1) - now).TotalSeconds);

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = resetIn.ToString();

        if (current > limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = resetIn.ToString();
            context.Response.ContentType = "application/json; charset=utf-8";

            var payload = ApiResponse.Fail(
                "Rate limit exceeded",
                new[] { $"Max {limit} requests per minute per IP." });

            await JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions);
            return;
        }

        await _next(context);
    }

    private bool IsBypassed(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        foreach (var bypass in _options.RateLimiting.BypassPaths)
        {
            if (!string.IsNullOrEmpty(bypass) &&
                path.StartsWith(bypass, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetClientIp(HttpContext context)
    {
        // Respect X-Forwarded-For if UseForwardedHeaders has populated Connection.RemoteIpAddress,
        // otherwise fall back to the raw header then the connection IP.
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private sealed class Counter
    {
        public DateTimeOffset WindowStart;
        public int Count;

        public Counter(DateTimeOffset windowStart)
        {
            WindowStart = windowStart;
            Count = 0;
        }
    }
}
