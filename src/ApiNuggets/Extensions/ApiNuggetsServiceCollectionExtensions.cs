using ApiNuggets.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApiNuggets.Extensions;

/// <summary>
/// DI registration entry point. Call <see cref="AddApiNuggets(IServiceCollection, Action{ApiNuggetsOptions}?)"/>
/// once in <c>Program.cs</c>.
/// </summary>
public static class ApiNuggetsServiceCollectionExtensions
{
    /// <summary>
    /// Registers ApiNuggets services and binds options from the
    /// <c>"ApiNuggets"</c> configuration section. A <paramref name="configure"/>
    /// delegate may override the bound values. JWT bearer authentication is
    /// registered automatically when <c>Jwt.Enable</c> is true.
    /// </summary>
    public static IServiceCollection AddApiNuggets(
        this IServiceCollection services,
        Action<ApiNuggetsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<ApiNuggetsOptions>()
            .BindConfiguration(ApiNuggetsOptions.SectionName)
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.Validate(
            ValidateOptions,
            "ApiNuggets configuration is invalid. When Jwt.Enable is true, Key (>= 16 chars), Issuer and Audience are required.");

        services.TryAddSingleton<IJwtTokenService, JwtTokenService>();
        services.TryAddSingleton<IAnalyticsStore, AnalyticsStore>();
        services.AddHttpContextAccessor();

        // Always register the bearer scheme + authorization services. If
        // Jwt.Enable is false the scheme simply stays dormant.
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerFromApiNuggets>();
        services.AddAuthorization();

        return services;
    }

    private static bool ValidateOptions(ApiNuggetsOptions o)
    {
        if (o.Jwt.Enable)
        {
            if (string.IsNullOrWhiteSpace(o.Jwt.Key) || o.Jwt.Key.Length < 16) return false;
            if (string.IsNullOrWhiteSpace(o.Jwt.Issuer)) return false;
            if (string.IsNullOrWhiteSpace(o.Jwt.Audience)) return false;
        }
        if (o.RateLimiting.Enable && o.RateLimiting.RequestsPerMinute <= 0) return false;
        if (o.Analytics.MaxLogEntries <= 0) return false;
        return true;
    }

    /// <summary>
    /// Applies ApiNuggets' JWT validation parameters onto the default JWT
    /// bearer scheme whenever <c>Jwt.Enable</c> is true. Running as a
    /// post-configure hook lets us read the resolved options without a
    /// chicken-and-egg problem against the options pipeline.
    /// </summary>
    private sealed class ConfigureJwtBearerFromApiNuggets : IPostConfigureOptions<JwtBearerOptions>
    {
        private readonly IOptionsMonitor<ApiNuggetsOptions> _apiNuggets;

        public ConfigureJwtBearerFromApiNuggets(IOptionsMonitor<ApiNuggetsOptions> apiNuggets)
        {
            _apiNuggets = apiNuggets;
        }

        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name is not null && name != JwtBearerDefaults.AuthenticationScheme) return;

            var jwt = _apiNuggets.CurrentValue.Jwt;
            if (!jwt.Enable) return;

            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = JwtTokenService.BuildValidationParameters(jwt);
        }
    }
}
