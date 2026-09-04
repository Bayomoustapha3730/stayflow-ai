using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Extensions;

public static class HealthChecksExtensions
{
    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready", "database"])
            .AddCheck<WhatsAppDependencyHealthCheck>("whatsapp", tags: ["ready", "external", "optional"])
            .AddCheck<MpesaDependencyHealthCheck>("mpesa", tags: ["ready", "external", "optional"]);

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

            var integrations = await dbContext.WhatsAppIntegrations
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDemoSeeded)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToListAsync(timeoutSource.Token);

            if (integrations.Count == 0)
            {
                return HealthCheckResult.Degraded("No configured WhatsApp integration is available for provider readiness evaluation.");
            }

            var hasProductionPending = false;

            foreach (var integration in integrations)
            {
                WhatsAppIntegrationHealthResponse result;
                try
                {
                    result = await integrationHealthService.CheckAsync(integration, timeoutSource.Token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    continue;
                }

                if (result.IsSendCapable)
                {
                    return HealthCheckResult.Healthy(result.Message);
                }

                hasProductionPending |= string.Equals(result.Status, "ProductionPending", StringComparison.Ordinal);
            }

            return hasProductionPending
                ? HealthCheckResult.Healthy("WhatsApp provider validation succeeded; production sending remains disabled pending activation.")
                : HealthCheckResult.Degraded("No configured WhatsApp integration is ready for provider use.");
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

internal sealed class MpesaDependencyHealthCheck(
    IMpesaHealthService mpesaHealthService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await mpesaHealthService.CheckAsync(cancellationToken);
            return result.Status switch
            {
                "Disabled" => HealthCheckResult.Healthy(result.Message),
                "ProviderReachable" => HealthCheckResult.Healthy(result.Message),
                _ => HealthCheckResult.Degraded(result.Message)
            };
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
