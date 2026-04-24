using ApiNuggets;
using ApiNuggets.Extensions;
using ApiNuggets.Models;
using ApiNuggets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// All ApiNuggets services (JWT, rate limiter, analytics, response wrapper,
// versioning header) with a single call. The options delegate is optional —
// appsettings.json is bound automatically from the "ApiNuggets" section.
builder.Services.AddApiNuggets(options =>
{
    options.Jwt.Enable = true;
    options.RateLimiting.Enable = true;
    options.Analytics.Enable = true;

    // Keep the OpenAPI document + Scalar UI out of response-wrapping and
    // rate-limiting so the doc loads raw JSON and the UI isn't throttled.
    options.ResponseWrapper.BypassPaths = new[] { "/api-nuggets", "/openapi", "/scalar" };
    options.RateLimiting.BypassPaths    = new[] { "/api-nuggets", "/openapi", "/scalar" };
});

// OpenAPI document generation (built-in since .NET 9). The document
// transformer declares the JWT bearer scheme so Scalar shows an
// "Authentication" panel you can paste your token into.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            In           = ParameterLocation.Header,
            Description  = "Paste the JWT returned by POST /api/v1/auth/login."
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Pipeline order matters. UseApiNuggets wires exception handling, response
// wrapping, rate limiting, and request logging. Authentication/authorization
// then plug in the normal ASP.NET Core way.
app.UseApiNuggets();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI document + Scalar reference UI.
//   /openapi/v1.json — the raw OpenAPI document
//   /scalar/v1       — the interactive Scalar UI
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title  = "ApiNuggets API";
    options.Theme  = ScalarTheme.BluePlanet;
});

// -------- Demo endpoints --------

// Anonymous "login" that mints a JWT for the supplied username. In a real
// app you'd verify credentials against your user store first.
app.MapPost("/api/v1/auth/login", (LoginRequest req, IJwtTokenService tokens) =>
{
    if (string.IsNullOrWhiteSpace(req.Username))
    {
        return Results.BadRequest(ApiResponse.Fail("Username is required"));
    }

    var roles = req.Username.Equals("admin", StringComparison.OrdinalIgnoreCase)
        ? new[] { "Admin", "User" }
        : new[] { "User" };

    var token = tokens.GenerateToken(req.Username, roles);
    return Results.Ok(new { token });
})
.WithTags("Auth");

// Raw payload — the response wrapper middleware will envelope this.
app.MapGet("/api/v1/weather", () =>
{
    var rng = Random.Shared;
    var forecast = Enumerable.Range(1, 5).Select(i => new
    {
        Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i)),
        TempC = rng.Next(-10, 35),
        Summary = "Generated"
    });
    return Results.Ok(forecast);
})
.WithTags("Demo");

// Auth-required endpoint.
app.MapGet("/api/v1/me", [Authorize] (HttpContext ctx) =>
{
    return Results.Ok(new
    {
        name = ctx.User.Identity?.Name,
        roles = ctx.User.Claims
            .Where(c => c.Type.EndsWith("/role", StringComparison.Ordinal) || c.Type == "role")
            .Select(c => c.Value)
            .ToArray()
    });
})
.WithTags("Auth");

// Role-gated endpoint to demo role support.
app.MapGet("/api/v1/admin/ping", [Authorize(Roles = "Admin")] () =>
    Results.Ok(new { pong = true, at = DateTimeOffset.UtcNow }))
    .WithTags("Auth");

// Endpoint that deliberately throws so you can see the exception envelope.
app.MapGet("/api/v1/boom", () =>
{
    throw new InvalidOperationException("Intentional boom for demo purposes.");
})
.WithTags("Demo");

// Endpoint that sleeps long enough to land in "slow requests".
app.MapGet("/api/v1/slow", async () =>
{
    await Task.Delay(750);
    return Results.Ok(new { slept = 750 });
})
.WithTags("Demo");

// ApiNuggets dashboard endpoints: /api-nuggets/dashboard (JSON) + /ui (HTML).
app.UseApiNuggetsDashboard();

app.Run();

internal sealed record LoginRequest(string Username, string? Password);
