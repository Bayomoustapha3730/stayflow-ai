using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StayFlow.Api.Data;
using StayFlow.Api.Services;

namespace StayFlow.Api.Extensions;

public static class HealthChecksExtensions
{
    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready", "database"])
            .AddCheck<WhatsAppDependencyHealthCheck>("whatsapp", tags: ["ready", "external", "optional"]);

        return services;
    }
}

internal sealed class DatabaseReadinessHealthCheck(
    ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(2));

        var canConnect = await dbContext.Database.CanConnectAsync(timeoutSource.Token);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}

internal sealed class WhatsAppDependencyHealthCheck(
    ApplicationDbContext dbContext,
    IWhatsAppIntegrationHealthService integrationHealthService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(2));

            var integration = await dbContext.WhatsAppIntegrations
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(timeoutSource.Token);

            if (integration is null)
            {
                return HealthCheckResult.Degraded();
            }

            var result = await integrationHealthService.CheckAsync(integration, timeoutSource.Token);
            return result.IsSendCapable
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded();
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded();
        }
        catch
        {
            return HealthCheckResult.Degraded();
        }
    }
}
