using Ratatosk.Core.Primitives;

namespace Ratatosk.API;

public record Response
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public object? Errors { get; init; }
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static Response Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static Response Fail(string message, object? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };

    public static Response FromResult(Result result) =>
        result.IsSuccess
            ? Ok()
            : Fail(result.Error ?? "Unexpected error");
}

public record Response<T> : Response
{
    public T? Data { get; init; }

    public static Response<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static new Response<T> Fail(string message, object? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };

    public static Response<T> FromResult(Result<T> result) =>
        result.IsSuccess
            ? Ok(result.Value!)
            : Fail(result.Error ?? "Unexpected error");
}
