using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data;

public static class SeedData
{
    public static readonly Guid DemoCompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoPropertyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FreePlanId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    public static readonly Guid StarterPlanId = Guid.Parse("90000000-0000-0000-0000-000000000002");
    public static readonly Guid ProfessionalPlanId = Guid.Parse("90000000-0000-0000-0000-000000000003");
    public static readonly Guid EnterprisePlanId = Guid.Parse("90000000-0000-0000-0000-000000000004");

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan
            {
                Id = FreePlanId,
                Name = "Free",
                DisplayName = "Free",
                Description = "Entry tier for early setup and light usage.",
                IsActive = true,
                IsEnterprise = false,
                SortOrder = 1,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new SubscriptionPlan
            {
                Id = StarterPlanId,
                Name = "Starter",
                DisplayName = "Starter",
                Description = "Small teams with moderate operational volume.",
                IsActive = true,
                IsEnterprise = false,
                SortOrder = 2,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new SubscriptionPlan
            {
                Id = ProfessionalPlanId,
                Name = "Professional",
                DisplayName = "Professional",
                Description = "Default production-safe plan for multi-property operations.",
                IsActive = true,
                IsEnterprise = false,
                SortOrder = 3,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new SubscriptionPlan
            {
                Id = EnterprisePlanId,
                Name = "Enterprise",
                DisplayName = "Enterprise",
                Description = "Custom enterprise plan with unlimited negotiated capacities.",
                IsActive = true,
                IsEnterprise = true,
                SortOrder = 4,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });

        modelBuilder.Entity<PlanEntitlement>().HasData(BuildPlanEntitlements());

        modelBuilder.Entity<Company>().HasData(new Company
        {
            Id = DemoCompanyId,
            Name = "StayFlow Demo Hosts",
            Slug = "stayflow-demo-hosts",
            NormalizedSlug = "STAYFLOW-DEMO-HOSTS",
            Status = "Active",
            LegalName = "StayFlow Demo Hosts Ltd",
            Email = "demo@stayflow.ai",
            PhoneNumber = "+254700000000",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });

        modelBuilder.Entity<Property>().HasData(new Property
        {
            Id = DemoPropertyId,
            CompanyId = DemoCompanyId,
            Name = "Demo Nairobi Apartment",
            AddressLine1 = "Westlands",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            Description = "Demo short-stay apartment configured for StayFlow AI onboarding.",
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });
    }

    private static IEnumerable<PlanEntitlement> BuildPlanEntitlements()
    {
        var now = SeededAt;
        var entitlements = new List<PlanEntitlement>();

        AddFeatures(entitlements, FreePlanId, now,
            [FeatureKeys.AiConcierge, FeatureKeys.HostCopilot, FeatureKeys.MultiProperty, FeatureKeys.ApiAccess],
            [FeatureKeys.WhatsApp, FeatureKeys.Analytics, FeatureKeys.CustomBranding, FeatureKeys.AdvancedIntegrations, FeatureKeys.PrioritySupport]);
        AddQuotas(entitlements, FreePlanId, now, new Dictionary<UsageMetric, long?>
        {
            [UsageMetric.Users] = 3,
            [UsageMetric.Properties] = 1,
            [UsageMetric.Reservations] = 200,
            [UsageMetric.AiRequests] = 1000,
            [UsageMetric.AiTokens] = 200000,
            [UsageMetric.WhatsAppMessages] = 200,
            [UsageMetric.ApiRequests] = 50000,
            [UsageMetric.StorageBytes] = 1073741824,
            [UsageMetric.FileUploads] = 500
        });

        AddFeatures(entitlements, StarterPlanId, now,
            [FeatureKeys.AiConcierge, FeatureKeys.HostCopilot, FeatureKeys.WhatsApp, FeatureKeys.MultiProperty, FeatureKeys.ApiAccess],
            [FeatureKeys.Analytics, FeatureKeys.CustomBranding, FeatureKeys.AdvancedIntegrations, FeatureKeys.PrioritySupport]);
        AddQuotas(entitlements, StarterPlanId, now, new Dictionary<UsageMetric, long?>
        {
            [UsageMetric.Users] = 10,
            [UsageMetric.Properties] = 5,
            [UsageMetric.Reservations] = 2000,
            [UsageMetric.AiRequests] = 10000,
            [UsageMetric.AiTokens] = 2000000,
            [UsageMetric.WhatsAppMessages] = 5000,
            [UsageMetric.ApiRequests] = 250000,
            [UsageMetric.StorageBytes] = 10737418240,
            [UsageMetric.FileUploads] = 5000
        });

        AddFeatures(entitlements, ProfessionalPlanId, now,
            [FeatureKeys.AiConcierge, FeatureKeys.HostCopilot, FeatureKeys.WhatsApp, FeatureKeys.Analytics, FeatureKeys.CustomBranding, FeatureKeys.AdvancedIntegrations, FeatureKeys.MultiProperty, FeatureKeys.PrioritySupport, FeatureKeys.ApiAccess],
            []);
        AddQuotas(entitlements, ProfessionalPlanId, now, new Dictionary<UsageMetric, long?>
        {
            [UsageMetric.Users] = 50,
            [UsageMetric.Properties] = 30,
            [UsageMetric.Reservations] = 15000,
            [UsageMetric.AiRequests] = 50000,
            [UsageMetric.AiTokens] = 20000000,
            [UsageMetric.WhatsAppMessages] = 50000,
            [UsageMetric.ApiRequests] = 2000000,
            [UsageMetric.StorageBytes] = 107374182400,
            [UsageMetric.FileUploads] = 50000
        });

        AddFeatures(entitlements, EnterprisePlanId, now,
            [FeatureKeys.AiConcierge, FeatureKeys.HostCopilot, FeatureKeys.WhatsApp, FeatureKeys.Analytics, FeatureKeys.CustomBranding, FeatureKeys.AdvancedIntegrations, FeatureKeys.MultiProperty, FeatureKeys.PrioritySupport, FeatureKeys.ApiAccess],
            []);
        AddQuotas(entitlements, EnterprisePlanId, now, new Dictionary<UsageMetric, long?>
        {
            [UsageMetric.Users] = null,
            [UsageMetric.Properties] = null,
            [UsageMetric.Reservations] = null,
            [UsageMetric.AiRequests] = null,
            [UsageMetric.AiTokens] = null,
            [UsageMetric.WhatsAppMessages] = null,
            [UsageMetric.ApiRequests] = null,
            [UsageMetric.StorageBytes] = null,
            [UsageMetric.FileUploads] = null
        });

        return entitlements;
    }

    private static void AddFeatures(List<PlanEntitlement> target, Guid planId, DateTimeOffset now, IReadOnlyCollection<string> enabled, IReadOnlyCollection<string> disabled)
    {
        foreach (var key in enabled)
        {
            target.Add(new PlanEntitlement
            {
                Id = DeterministicGuid($"{planId:D}:feature:{key}:enabled"),
                SubscriptionPlanId = planId,
                Key = key,
                IsEnabled = true,
                IsUnlimited = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        foreach (var key in disabled)
        {
            target.Add(new PlanEntitlement
            {
                Id = DeterministicGuid($"{planId:D}:feature:{key}:disabled"),
                SubscriptionPlanId = planId,
                Key = key,
                IsEnabled = false,
                IsUnlimited = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static void AddQuotas(List<PlanEntitlement> target, Guid planId, DateTimeOffset now, IReadOnlyDictionary<UsageMetric, long?> quotas)
    {
        foreach (var quota in quotas)
        {
            target.Add(new PlanEntitlement
            {
                Id = DeterministicGuid($"{planId:D}:quota:{quota.Key}"),
                SubscriptionPlanId = planId,
                Key = quota.Key.ToQuotaEntitlementKey(),
                IsEnabled = true,
                QuotaLimit = quota.Value,
                IsUnlimited = quota.Value is null,
                Unit = "count",
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = MD5.HashData(bytes);
        return new Guid(hash);
    }
}
