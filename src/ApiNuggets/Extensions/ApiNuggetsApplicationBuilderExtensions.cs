using ApiNuggets.Dashboard;
using ApiNuggets.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiNuggets.Extensions;

/// <summary>
/// Pipeline registration for ApiNuggets. Order is important — call
/// <see cref="UseApiNuggets"/> early (before routing/endpoint execution)
/// and <see cref="UseApiNuggetsDashboard"/> after <c>UseRouting</c>.
/// </summary>
public static class ApiNuggetsApplicationBuilderExtensions
{
    /// <summary>
    /// Inserts the full ApiNuggets middleware stack:
    /// exception handling → response wrapping → rate limiting → request logging.
    /// Call this once, before the endpoint middleware.
    /// </summary>
    public static IApplicationBuilder UseApiNuggets(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.ApplicationServices
            .GetRequiredService<IOptions<ApiNuggetsOptions>>()
            .Value;

        // Outermost: catches everything.
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // Response wrapping wraps the body written downstream.
        if (options.ResponseWrapper.Enable)
        {
            app.UseMiddleware<ResponseWrapperMiddleware>();
        }

        // Versioning header — cheap, stamp on every response.
        if (options.Versioning.Enable)
        {
            var headerName = options.Versioning.HeaderName;
            var defaultVersion = options.Versioning.DefaultVersion;
            app.Use(async (ctx, next) =>
            {
                ctx.Response.OnStarting(() =>
                {
                    if (!ctx.Response.Headers.ContainsKey(headerName))
                    {
                        ctx.Response.Headers[headerName] = defaultVersion;
                    }
                    return Task.CompletedTask;
                });
                await next();
            });
        }

        if (options.RateLimiting.Enable)
        {
            app.UseMiddleware<RateLimitingMiddleware>();
        }

        // Logging last so its stopwatch reflects the real work.
        app.UseMiddleware<RequestLoggingMiddleware>();

        return app;
    }

    /// <summary>
    /// Maps <c>/api-nuggets/dashboard</c> (JSON) and optionally
    /// <c>/api-nuggets/dashboard/ui</c> (embedded HTML) onto the app.
    /// Requires <c>UseRouting</c> to have been called already (or for the host
    /// to provide implicit routing, as minimal APIs do).
    /// </summary>
    public static IApplicationBuilder UseApiNuggetsDashboard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app is IEndpointRouteBuilder endpointBuilder)
        {
            endpointBuilder.MapApiNuggetsDashboard();
            return app;
        }

        // Fall back to a terminal branch for non-endpoint pipelines.
        app.UseEndpoints(endpoints => endpoints.MapApiNuggetsDashboard());
        return app;
    }
}
