namespace StayFlow.Api.Configuration;

public sealed class ProductionHardeningOptions
{
    public const string SectionName = "ProductionHardening";

    public CorrelationOptions Correlation { get; init; } = new();
    public ResilienceOptions Resilience { get; init; } = new();
    public SecurityOptions Security { get; init; } = new();
    public HealthOptions Health { get; init; } = new();

    public sealed class CorrelationOptions
    {
        public string HeaderName { get; init; } = "X-Correlation-Id";
        public int MaximumLength { get; init; } = 64;
    }

    public sealed class ResilienceOptions
    {
        public int RetryCount { get; init; } = 2;
        public int BaseDelayMilliseconds { get; init; } = 250;
        public int TimeoutSeconds { get; init; } = 20;
        public int CircuitBreakDurationSeconds { get; init; } = 30;
    }

    public sealed class SecurityOptions
    {
        public bool EnableHsts { get; init; } = true;
        public int MaximumRequestBodyBytes { get; init; } = 1_048_576;
        public string[] AllowedOrigins { get; init; } = [];
    }

    public sealed class HealthOptions
    {
        public int ExternalDependencyTimeoutSeconds { get; init; } = 2;
    }
}
