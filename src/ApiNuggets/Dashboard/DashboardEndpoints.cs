using System.Reflection;
using ApiNuggets.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiNuggets.Dashboard;

/// <summary>Maps the ApiNuggets dashboard endpoints onto the app.</summary>
internal static class DashboardEndpoints
{
    private const string HtmlResourceName = "ApiNuggets.Dashboard.dashboard.html";

    public static IEndpointRouteBuilder MapApiNuggetsDashboard(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<ApiNuggetsOptions>>().Value.Dashboard;
        var basePath = NormalizePath(options.Path);

        var jsonRoute = endpoints.MapGet(basePath, async (HttpContext ctx, IAnalyticsStore store) =>
        {
            var snapshot = store.GetSnapshot();
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsJsonAsync(snapshot);
        }).WithName("ApiNuggetsDashboardData");

        if (options.RequireAuthentication)
        {
            jsonRoute.RequireAuthorization();
        }
        else
        {
            jsonRoute.AllowAnonymous();
        }

        if (options.EnableUi)
        {
            var uiRoute = endpoints.MapGet($"{basePath}/ui", async (HttpContext ctx) =>
            {
                var html = LoadEmbeddedHtml();
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.WriteAsync(html);
            }).WithName("ApiNuggetsDashboardUi");

            if (options.RequireAuthentication)
            {
                uiRoute.RequireAuthorization();
            }
            else
            {
                uiRoute.AllowAnonymous();
            }
        }

        return endpoints;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/api-nuggets/dashboard";
        path = path.Trim();
        if (!path.StartsWith('/')) path = "/" + path;
        if (path.Length > 1 && path.EndsWith('/')) path = path.TrimEnd('/');
        return path;
    }

    private static string LoadEmbeddedHtml()
    {
        var asm = typeof(DashboardEndpoints).GetTypeInfo().Assembly;
        using var stream = asm.GetManifestResourceStream(HtmlResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded dashboard resource '{HtmlResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
