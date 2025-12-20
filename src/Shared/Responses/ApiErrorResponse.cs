namespace Shared.Responses;

/// <summary>
/// Standard API error response.
/// </summary>
public record ApiErrorResponse
{
    public required string Message { get; init; }
    public string? Code { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    public string? TraceId { get; init; }

    public static ApiErrorResponse NotFound(string message = "Resource not found", string? traceId = null) => new()
    {
        Message = message,
        Code = "NOT_FOUND",
        TraceId = traceId
    };

    public static ApiErrorResponse BadRequest(string message, IReadOnlyDictionary<string, string[]>? errors = null, string? traceId = null) => new()
    {
        Message = message,
        Code = "BAD_REQUEST",
        Errors = errors,
        TraceId = traceId
    };

    public static ApiErrorResponse Conflict(string message, string? traceId = null) => new()
    {
        Message = message,
        Code = "CONFLICT",
        TraceId = traceId
    };

    public static ApiErrorResponse InternalError(string message, string? traceId = null) => new()
    {
        Message = message,
        Code = "INTERNAL_ERROR",
        TraceId = traceId
    };
}
