using System.Net;
using System.Text.Json;

namespace StayFlow.Api.Middleware;

public sealed class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            throw exception;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = "https://httpstatuses.com/500",
            title = "An unexpected error occurred.",
            status = context.Response.StatusCode,
            detail = environment.IsDevelopment() ? exception.Message : null,
            traceId = context.TraceIdentifier
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, problem);
    }
}
