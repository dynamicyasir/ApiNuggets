using System.Text.Json;
using ApiNuggets.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ApiNuggets.Middleware;

/// <summary>
/// Buffers the downstream response and — if it is a successful JSON body
/// that is not already wrapped — rewrites it into the standard
/// <see cref="ApiResponse{T}"/> envelope.
/// </summary>
/// <remarks>
/// Only responses with <c>application/json</c> content type and a 2xx
/// status are touched. Already-wrapped payloads (identified by a
/// <c>success</c> property at the root) are passed through. Dashboard
/// paths in <see cref="ResponseWrapperOptions.BypassPaths"/> are skipped.
/// </remarks>
internal sealed class ResponseWrapperMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ApiNuggetsOptions _options;

    public ResponseWrapperMiddleware(RequestDelegate next, IOptions<ApiNuggetsOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.ResponseWrapper.Enable || IsBypassed(context))
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Position = 0;

            if (ShouldWrap(context, buffer, out var rawJson))
            {
                var wrapped = ApiResponse.Ok(rawJson, string.Empty);
                context.Response.Body = originalBody;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.Remove("Content-Length");

                await using var writer = new Utf8JsonWriter(originalBody);
                WriteEnvelope(writer, wrapped);
                await writer.FlushAsync();
            }
            else
            {
                context.Response.Body = originalBody;
                if (buffer.Length > 0)
                {
                    buffer.Position = 0;
                    await buffer.CopyToAsync(originalBody);
                }
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private bool IsBypassed(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        foreach (var bypass in _options.ResponseWrapper.BypassPaths)
        {
            if (!string.IsNullOrEmpty(bypass) &&
                path.StartsWith(bypass, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ShouldWrap(HttpContext context, MemoryStream buffer, out JsonElement body)
    {
        body = default;

        var status = context.Response.StatusCode;
        if (status < 200 || status >= 300) return false;
        if (buffer.Length == 0) return false;

        var contentType = context.Response.ContentType ?? string.Empty;
        if (!contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            buffer.Position = 0;
            using var doc = JsonDocument.Parse(buffer, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = doc.RootElement;

            // Already wrapped? Pass through.
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("success", out _) &&
                root.TryGetProperty("message", out _))
            {
                return false;
            }

            body = root.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void WriteEnvelope(Utf8JsonWriter writer, ApiResponse response)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("success", response.Success);
        writer.WriteString("message", response.Message);

        writer.WritePropertyName("data");
        if (response.Data is JsonElement element)
        {
            element.WriteTo(writer);
        }
        else if (response.Data is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            JsonSerializer.Serialize(writer, response.Data, JsonOptions);
        }

        writer.WritePropertyName("errors");
        writer.WriteStartArray();
        foreach (var err in response.Errors)
        {
            writer.WriteStringValue(err);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
