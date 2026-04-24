using System.Text.Json;
using ApiNuggets.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiNuggets.Middleware;

/// <summary>
/// Outermost ApiNuggets middleware. Catches any unhandled exception thrown
/// deeper in the pipeline and returns the standard error envelope.
/// </summary>
internal sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // Can't rewrite — rethrow so the host's default handler can deal with it.
                throw;
            }

            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception ex)
    {
        // ASP.NET Core's HttpResponse has no Clear() — reset the bits we care
        // about explicitly.
        context.Response.Headers.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";

        var errors = _env.IsDevelopment()
            ? new[] { ex.Message, ex.GetType().FullName ?? "Exception" }
            : Array.Empty<string>();

        var payload = ApiResponse.Fail("Error occurred", errors);
        await JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions);
    }
}
