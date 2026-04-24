namespace ApiNuggets;

/// <summary>
/// Root configuration object for ApiNuggets. Bind from the
/// <c>"ApiNuggets"</c> section of <c>appsettings.json</c> or configure
/// inline via <c>AddApiNuggets(o => ...)</c>.
/// </summary>
public sealed class ApiNuggetsOptions
{
    /// <summary>The configuration section name ApiNuggets binds against.</summary>
    public const string SectionName = "ApiNuggets";

    public JwtOptions Jwt { get; set; } = new();
    public RateLimitingOptions RateLimiting { get; set; } = new();
    public AnalyticsOptions Analytics { get; set; } = new();
    public ResponseWrapperOptions ResponseWrapper { get; set; } = new();
    public DashboardOptions Dashboard { get; set; } = new();
    public VersioningOptions Versioning { get; set; } = new();
}

/// <summary>JWT bearer authentication options.</summary>
public sealed class JwtOptions
{
    public bool Enable { get; set; }

    /// <summary>The symmetric signing key. Must be at least 32 characters for HS256.</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ApiNuggets";
    public string Audience { get; set; } = "ApiNuggets";

    /// <summary>Token lifetime in minutes. Defaults to 60.</summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>Clock skew tolerance in seconds when validating. Defaults to 30.</summary>
    public int ClockSkewSeconds { get; set; } = 30;
}

/// <summary>In-memory per-IP rate limiting.</summary>
public sealed class RateLimitingOptions
{
    public bool Enable { get; set; }

    /// <summary>Maximum requests per IP per minute. Defaults to 60.</summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>Paths (prefix match) that should bypass rate limiting.</summary>
    public string[] BypassPaths { get; set; } = new[] { "/api-nuggets" };
}

/// <summary>Analytics capture + dashboard options.</summary>
public sealed class AnalyticsOptions
{
    public bool Enable { get; set; }

    /// <summary>Request duration (ms) above which the request is flagged slow. Defaults to 500.</summary>
    public int SlowRequestThresholdMs { get; set; } = 500;

    /// <summary>Max log entries held in the ring buffer. Defaults to 1000.</summary>
    public int MaxLogEntries { get; set; } = 1000;
}

/// <summary>Standard API response wrapping behaviour.</summary>
public sealed class ResponseWrapperOptions
{
    /// <summary>Wrap successful JSON responses in the standard envelope. Defaults to true.</summary>
    public bool Enable { get; set; } = true;

    /// <summary>Path prefixes that should be left un-wrapped (e.g. the dashboard itself).</summary>
    public string[] BypassPaths { get; set; } = new[] { "/api-nuggets" };
}

/// <summary>Dashboard endpoint options.</summary>
public sealed class DashboardOptions
{
    /// <summary>Base path for the dashboard endpoints. Defaults to "/api-nuggets/dashboard".</summary>
    public string Path { get; set; } = "/api-nuggets/dashboard";

    /// <summary>When true the embedded HTML dashboard is served at {Path}/ui. Defaults to true.</summary>
    public bool EnableUi { get; set; } = true;

    /// <summary>Require an authenticated user to view the dashboard. Defaults to false for local dev.</summary>
    public bool RequireAuthentication { get; set; }
}

/// <summary>API versioning options. v1 uses a simple URL-segment convention.</summary>
public sealed class VersioningOptions
{
    public bool Enable { get; set; } = true;

    /// <summary>Default version exposed as <c>X-Api-Version</c> header. Defaults to "1.0".</summary>
    public string DefaultVersion { get; set; } = "1.0";

    /// <summary>Name of the response header that advertises the version.</summary>
    public string HeaderName { get; set; } = "X-Api-Version";
}
