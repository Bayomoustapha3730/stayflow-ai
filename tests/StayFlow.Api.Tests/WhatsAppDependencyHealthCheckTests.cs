using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Extensions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppDependencyHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_SeededDemoAndValidRealIntegration_IgnoresDemo()
    {
        var demo = CreateIntegration(isDemoSeeded: true);
        var real = CreateIntegration();
        var health = await CheckAsync([demo, real],
            (real.Id, new WhatsAppIntegrationHealthResponse { Status = "Healthy", IsSendCapable = true }));

        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal([real.Id], health.EvaluatedIntegrationIds);
    }

    [Fact]
    public async Task CheckHealthAsync_OnlySeededDemo_ReturnsDegradedWithoutEvaluatingDemo()
    {
        var demo = CreateIntegration(isDemoSeeded: true);
        var health = await CheckAsync([demo]);

        Assert.Equal(HealthStatus.Degraded, health.Status);
        Assert.Contains("No configured WhatsApp integration", health.Description, StringComparison.Ordinal);
        Assert.Empty(health.EvaluatedIntegrationIds);
    }

    [Fact]
    public async Task CheckHealthAsync_HealthyIntegration_ReturnsHealthy()
    {
        var integration = CreateIntegration();
        var health = await CheckAsync([integration],
            (integration.Id, new WhatsAppIntegrationHealthResponse { Status = "Healthy", IsSendCapable = true }));

        Assert.Equal(HealthStatus.Healthy, health.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ProductionPendingIntegration_ReturnsHealthyReadiness()
    {
        var integration = CreateIntegration();
        var health = await CheckAsync([integration],
            (integration.Id, new WhatsAppIntegrationHealthResponse { Status = "ProductionPending", IsSendCapable = false }));

        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Contains("production sending remains disabled", health.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_ConfigurationIncompleteIntegration_ReturnsDegraded()
    {
        var integration = CreateIntegration();
        var health = await CheckAsync([integration],
            (integration.Id, new WhatsAppIntegrationHealthResponse { Status = "ConfigurationIncomplete", IsSendCapable = false }));

        Assert.Equal(HealthStatus.Degraded, health.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_MultipleRealIntegrations_EvaluatesAllAndHealthyWins()
    {
        var unhealthy = CreateIntegration();
        var healthy = CreateIntegration();
        unhealthy.CreatedAt = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        healthy.CreatedAt = new DateTimeOffset(2026, 9, 4, 11, 0, 0, TimeSpan.Zero);
        var health = await CheckAsync([unhealthy, healthy],
            (unhealthy.Id, new WhatsAppIntegrationHealthResponse { Status = "ConfigurationIncomplete", IsSendCapable = false }),
            (healthy.Id, new WhatsAppIntegrationHealthResponse { Status = "Healthy", IsSendCapable = true }));

        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal([unhealthy.Id, healthy.Id], health.EvaluatedIntegrationIds);
    }

    private static async Task<(HealthStatus Status, string Description, IReadOnlyList<Guid> EvaluatedIntegrationIds)> CheckAsync(
        IReadOnlyCollection<WhatsAppIntegration> integrations,
        params (Guid IntegrationId, WhatsAppIntegrationHealthResponse Result)[] results)
    {
        var healthService = new FakeIntegrationHealthService(results.ToDictionary(item => item.IntegrationId, item => item.Result));
        var services = new ServiceCollection();
        var databaseName = $"whatsapp-health-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IWhatsAppIntegrationHealthService>(healthService);
        services.AddApplicationHealthChecks();

        await using var provider = services.BuildServiceProvider();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var explicitCreatedAt = integrations
                .Where(item => item.CreatedAt != default)
                .ToDictionary(item => item.Id, item => item.CreatedAt);
            await dbContext.WhatsAppIntegrations.AddRangeAsync(integrations);
            await dbContext.SaveChangesAsync();

            foreach (var integration in integrations)
            {
                if (explicitCreatedAt.TryGetValue(integration.Id, out var createdAt))
                {
                    integration.CreatedAt = createdAt;
                }
            }

            await dbContext.SaveChangesAsync();
            await dbContext.WhatsAppIntegrations.CountAsync();
        }

        await using (var healthScope = provider.CreateAsyncScope())
        {
            var registration = healthScope.ServiceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
                .Single(item => item.Name == "whatsapp");
            var check = registration.Factory(healthScope.ServiceProvider);
            var result = await check.CheckHealthAsync(new HealthCheckContext { Registration = registration });

            return (result.Status, result.Description ?? string.Empty, healthService.EvaluatedIntegrationIds);
        }
    }

    private static WhatsAppIntegration CreateIntegration(bool isDemoSeeded = false) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        DisplayName = isDemoSeeded ? "Demo WhatsApp Concierge" : "Configured integration",
        IsActive = true,
        IsDemoSeeded = isDemoSeeded
    };

    private sealed class FakeIntegrationHealthService(
        IReadOnlyDictionary<Guid, WhatsAppIntegrationHealthResponse> results) : IWhatsAppIntegrationHealthService
    {
        public List<Guid> EvaluatedIntegrationIds { get; } = [];

        public Task<WhatsAppIntegrationHealthResponse> CheckAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
        {
            EvaluatedIntegrationIds.Add(integration.Id);
            return Task.FromResult(results[integration.Id]);
        }
    }
}