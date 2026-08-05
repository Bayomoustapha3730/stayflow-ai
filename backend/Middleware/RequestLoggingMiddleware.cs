using System.Diagnostics;

namespace StayFlow.Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var skipDetailed = path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/hubs/conversations", StringComparison.OrdinalIgnoreCase);

        var started = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
                ? value?.ToString()
                : null;
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

            if (!skipDetailed)
            {
                logger.LogInformation(
                    "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMilliseconds}ms TraceId={TraceId} CorrelationId={CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    Math.Round(elapsedMs, 2),
                    traceId,
                    correlationId);
            }
            else
            {
                logger.LogDebug(
                    "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    Math.Round(elapsedMs, 2));
            }
        }
    }
}
