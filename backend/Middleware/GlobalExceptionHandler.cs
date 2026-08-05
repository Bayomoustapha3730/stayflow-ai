using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Exceptions;

namespace StayFlow.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail, errorCode) = MapException(exception);
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
            ? value?.ToString()
            : null;

        logger.LogError(
            exception,
            "Request failed with status {StatusCode}. Method={Method} Path={Path} TraceId={TraceId} CorrelationId={CorrelationId}",
            status,
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId,
            correlationId);

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problem.Extensions["errorCode"] = errorCode;
        }

        if (environment.IsDevelopment())
        {
            problem.Extensions["requestId"] = httpContext.TraceIdentifier;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static (int Status, string Title, string Detail, string? ErrorCode) MapException(Exception exception)
    {
        return exception switch
        {
            DomainValidationException domainValidation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "One or more request values are invalid.",
                domainValidation.ErrorCode),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required to access this resource.",
                "unauthorized"),
            ForbiddenOperationException forbidden => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You are not allowed to perform this operation.",
                forbidden.ErrorCode),
            ResourceNotFoundException missing => (
                StatusCodes.Status404NotFound,
                "Not found",
                "The requested resource could not be found.",
                missing.ErrorCode),
            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Conflict",
                "The request conflicts with the current resource state.",
                conflict.ErrorCode),
            ExternalDependencyException external => (
                StatusCodes.Status503ServiceUnavailable,
                "Dependency unavailable",
                "A required upstream dependency is currently unavailable.",
                external.ErrorCode),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Bad request",
                "The request could not be processed.",
                "bad_request"),
            OperationCanceledException => (
                499,
                "Request canceled",
                "The request was canceled before completion.",
                "request_canceled"),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "Unexpected error",
                "An unexpected server error occurred.",
                "unexpected_error")
        };
    }
}
