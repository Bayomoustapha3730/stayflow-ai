using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace StayFlow.Api.Extensions;

public static class HealthCheckResponseWriter
{
    public static Task WriteMinimalAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString()
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
