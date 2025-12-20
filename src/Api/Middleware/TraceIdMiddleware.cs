using System.Diagnostics;

namespace Api.Middleware;

/// <summary>
/// Middleware that adds X-Trace-Id header to all responses for correlation with telemetry.
/// </summary>
public class TraceIdMiddleware
{
    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get trace ID from current activity or fall back to HttpContext.TraceIdentifier
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        // Add header before response starts
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Extension methods for TraceIdMiddleware.
/// </summary>
public static class TraceIdMiddlewareExtensions
{
    public static IApplicationBuilder UseTraceId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TraceIdMiddleware>();
    }
}
