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

    public static ApiErrorResponse NotFound(string message = "Resource not found") => new()
    {
        Message = message,
        Code = "NOT_FOUND"
    };

    public static ApiErrorResponse BadRequest(string message, IReadOnlyDictionary<string, string[]>? errors = null) => new()
    {
        Message = message,
        Code = "BAD_REQUEST",
        Errors = errors
    };

    public static ApiErrorResponse Conflict(string message) => new()
    {
        Message = message,
        Code = "CONFLICT"
    };

    public static ApiErrorResponse InternalError(string message, string? traceId = null) => new()
    {
        Message = message,
        Code = "INTERNAL_ERROR",
        TraceId = traceId
    };
}
