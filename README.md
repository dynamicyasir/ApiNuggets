# ApiNuggets

[![NuGet](https://img.shields.io/nuget/v/ApiNuggets.svg)](https://www.nuget.org/packages/ApiNuggets)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Plug-and-play ASP.NET Core toolkit. JWT auth, rate limiting, exception handling,
response wrapping, request logging, and a built-in analytics dashboard — all
with two lines of setup.

## Install

```bash
dotnet add package ApiNuggets
```

## Quick start

```csharp
using ApiNuggets.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiNuggets(options =>
{
    options.Jwt.Enable = true;
    options.RateLimiting.Enable = true;
    options.Analytics.Enable = true;
});

var app = builder.Build();

app.UseApiNuggets();
app.UseAuthentication();
app.UseAuthorization();

app.UseApiNuggetsDashboard();

app.Run();
```

## Configuration via `appsettings.json`

```json
{
  "ApiNuggets": {
    "Jwt": {
      "Enable": true,
      "Issuer": "ApiNuggets",
      "Audience": "ApiNuggetsClients",
      "Key": "replace-with-32+character-secret",
      "ExpirationMinutes": 60
    },
    "RateLimiting": {
      "Enable": true,
      "PermitLimit": 100,
      "WindowSeconds": 60
    },
    "Analytics": {
      "Enable": true,
      "SlowRequestThresholdMs": 500
    }
  }
}
```

## What you get

- **JWT authentication** with role support (`IJwtTokenService.GenerateToken(...)`)
- **Per-IP rate limiting** with `X-RateLimit-*` headers and `429` + `Retry-After`
- **Global exception handling** returning a clean `ApiResponse` envelope
- **Response wrapping** for consistent `{ success, message, data, errors }` JSON
- **Request logging** with method, path, status, duration, user
- **Live analytics dashboard** at `/api-nuggets/dashboard/ui`
- **API versioning header** (`X-Api-Version`)

## Sample project

See [`samples/ApiNuggets.Sample`](samples/ApiNuggets.Sample) for a full working
demo with Scalar UI, OpenAPI, login endpoint, and `.http` test file.

## License

MIT — see [LICENSE](LICENSE).
