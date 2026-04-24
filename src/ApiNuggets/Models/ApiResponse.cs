using System.Text.Json.Serialization;

namespace ApiNuggets.Models;

/// <summary>
/// The standard ApiNuggets response envelope. Use the static factories
/// (<see cref="Ok(T?, string)"/>, <see cref="Fail(string, IEnumerable{string}?)"/>)
/// to produce instances instead of <c>new ApiResponse&lt;T&gt;(...)</c>.
/// </summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyOrder(0)]
    public bool Success { get; init; }

    [JsonPropertyOrder(1)]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyOrder(2)]
    public T? Data { get; init; }

    [JsonPropertyOrder(3)]
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse<T> Ok(T? data, string message = "")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null)
        => new()
        {
            Success = false,
            Message = message,
            Errors = errors is null ? Array.Empty<string>() : errors.ToArray()
        };
}

/// <summary>Non-generic convenience wrapper, used by the exception middleware.</summary>
public sealed class ApiResponse
{
    [JsonPropertyOrder(0)]
    public bool Success { get; init; }

    [JsonPropertyOrder(1)]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyOrder(2)]
    public object? Data { get; init; }

    [JsonPropertyOrder(3)]
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse Ok(object? data = null, string message = "")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse Fail(string message, IEnumerable<string>? errors = null)
        => new()
        {
            Success = false,
            Message = message,
            Errors = errors is null ? Array.Empty<string>() : errors.ToArray()
        };
}
