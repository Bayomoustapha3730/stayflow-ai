using System.Diagnostics;
using StayFlow.Api.Middleware;

namespace StayFlow.Api.Services;

public sealed class OutboundCorrelationHandler(
    ICurrentTenantContext currentTenantContext) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = currentTenantContext.CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationId)
            && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        if (Activity.Current is { } activity
            && !request.Headers.Contains("traceparent"))
        {
            request.Headers.TryAddWithoutValidation("traceparent", activity.Id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
