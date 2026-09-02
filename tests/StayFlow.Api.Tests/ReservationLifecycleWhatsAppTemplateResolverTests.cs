using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationLifecycleWhatsAppTemplateResolverTests
{
    [Fact]
    public async Task TemplateResolver_DoesNotCrossTenantBoundary()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var integrationA = Guid.NewGuid();
        var integrationB = Guid.NewGuid();
        var repository = new FakeMappingRepository([
            NewMapping(companyB, integrationB, ReservationLifecycleEventType.PreArrival, "en", NewTemplate(companyB, integrationB, "en", "APPROVED"))
        ]);
        var resolver = new ReservationLifecycleWhatsAppTemplateResolver(repository);

        var result = await resolver.ResolveAsync(companyA, integrationA, ReservationLifecycleEventType.PreArrival, "en", NewReservation(companyA), NewProperty(companyA), NewGuest(companyA, "en"), CancellationToken.None);

        Assert.False(result.Resolved);
        Assert.Contains("No enabled", result.BlockedReason);
    }

    [Fact]
    public async Task TemplateResolver_DifferentIntegrationDoesNotResolve()
    {
        var companyId = Guid.NewGuid();
        var requestedIntegration = Guid.NewGuid();
        var otherIntegration = Guid.NewGuid();
        var repository = new FakeMappingRepository([
            NewMapping(companyId, otherIntegration, ReservationLifecycleEventType.ArrivalDay, "en", NewTemplate(companyId, otherIntegration, "en", "APPROVED"))
        ]);
        var resolver = new ReservationLifecycleWhatsAppTemplateResolver(repository);

        var result = await resolver.ResolveAsync(companyId, requestedIntegration, ReservationLifecycleEventType.ArrivalDay, "en", NewReservation(companyId), NewProperty(companyId), NewGuest(companyId, "en"), CancellationToken.None);

        Assert.False(result.Resolved);
        Assert.Contains("No enabled", result.BlockedReason);
    }

    [Fact]
    public async Task TemplateResolver_UsesPreferredLanguageWhenMapped()
    {
        var companyId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var fallbackTemplate = NewTemplate(companyId, integrationId, "en", "APPROVED");
        var preferredTemplate = NewTemplate(companyId, integrationId, "fr", "APPROVED");
        var repository = new FakeMappingRepository([
            NewMapping(companyId, integrationId, ReservationLifecycleEventType.InStay, string.Empty, fallbackTemplate),
            NewMapping(companyId, integrationId, ReservationLifecycleEventType.InStay, "fr", preferredTemplate)
        ]);
        var resolver = new ReservationLifecycleWhatsAppTemplateResolver(repository);

        var result = await resolver.ResolveAsync(companyId, integrationId, ReservationLifecycleEventType.InStay, "fr", NewReservation(companyId), NewProperty(companyId), NewGuest(companyId, "fr"), CancellationToken.None);

        Assert.True(result.Resolved);
        Assert.Equal(preferredTemplate.Id, result.Template?.Id);
    }

    [Fact]
    public async Task TemplateResolver_DoesNotSelectArbitraryApprovedTemplate()
    {
        var companyId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var repository = new FakeMappingRepository([
            NewMapping(companyId, integrationId, ReservationLifecycleEventType.CheckoutDay, "en", NewTemplate(companyId, integrationId, "en", "APPROVED"))
        ]);
        var resolver = new ReservationLifecycleWhatsAppTemplateResolver(repository);

        var result = await resolver.ResolveAsync(companyId, integrationId, ReservationLifecycleEventType.PostStay, "en", NewReservation(companyId), NewProperty(companyId), NewGuest(companyId, "en"), CancellationToken.None);

        Assert.False(result.Resolved);
        Assert.Contains("No enabled", result.BlockedReason);
    }

    [Fact]
    public async Task TemplateResolver_UnapprovedTemplateDoesNotResolve()
    {
        var companyId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var repository = new FakeMappingRepository([
            NewMapping(companyId, integrationId, ReservationLifecycleEventType.PostStay, "en", NewTemplate(companyId, integrationId, "en", "REJECTED"))
        ]);
        var resolver = new ReservationLifecycleWhatsAppTemplateResolver(repository);

        var result = await resolver.ResolveAsync(companyId, integrationId, ReservationLifecycleEventType.PostStay, "en", NewReservation(companyId), NewProperty(companyId), NewGuest(companyId, "en"), CancellationToken.None);

        Assert.False(result.Resolved);
        Assert.Contains("not approved", result.BlockedReason);
    }

    [Fact]
    public async Task TemplateResolver_BindsConfiguredParametersInOrder()
    {
        var companyId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var template = NewTemplate(companyId, integrationId, "en", "APPROVED", variableCount: 3);
        var repository = new FakeMappingRepository([
            NewMapping(companyId, integrationId, ReservationLifecycleEventType.PreArrival, "en", template, "GuestFirstName,PropertyName,CheckInDate")
        ]);
        var resolver = new ReservationLifecycleWhatsAppTemplateResolver(repository);

        var result = await resolver.ResolveAsync(companyId, integrationId, ReservationLifecycleEventType.PreArrival, "en", NewReservation(companyId), NewProperty(companyId), NewGuest(companyId, "en"), CancellationToken.None);

        Assert.True(result.Resolved);
        Assert.Equal(["Ada", "Demo Property", "August 10, 2026"], result.Variables);
    }

    private static ReservationLifecycleWhatsAppTemplateMapping NewMapping(Guid companyId, Guid integrationId, ReservationLifecycleEventType eventType, string languageCode, WhatsAppTemplate template, string parameterBindings = "")
    {
        return new ReservationLifecycleWhatsAppTemplateMapping
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            WhatsAppIntegrationId = integrationId,
            WhatsAppTemplateId = template.Id,
            JourneyEventType = eventType,
            LanguageCode = languageCode,
            IsEnabled = true,
            ParameterBindings = parameterBindings,
            WhatsAppTemplate = template
        };
    }

    private static WhatsAppTemplate NewTemplate(Guid companyId, Guid integrationId, string languageCode, string status, int variableCount = 0)
    {
        return new WhatsAppTemplate
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            WhatsAppIntegrationId = integrationId,
            Name = $"tenant_template_{Guid.NewGuid():N}",
            LanguageCode = languageCode,
            Status = status,
            BodyText = "Hi {{1}}",
            VariableCount = variableCount,
            IsActive = true
        };
    }

    private static Reservation NewReservation(Guid companyId)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = Guid.NewGuid(),
            PrimaryGuestId = Guid.NewGuid(),
            ReservationSource = "Manual",
            CheckInDate = new DateOnly(2026, 8, 10),
            CheckOutDate = new DateOnly(2026, 8, 14),
            Status = ReservationStatus.Confirmed,
            IsActive = true
        };
    }

    private static Property NewProperty(Guid companyId)
    {
        return new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Demo Property", AddressLine1 = "Road", City = "Nairobi", CountryCode = "KE", TimeZone = "Africa/Nairobi", IsActive = true };
    }

    private static Guest NewGuest(Guid companyId, string language)
    {
        return new Guest { Id = Guid.NewGuid(), CompanyId = companyId, FirstName = "Ada", LastName = "Guest", PreferredLanguage = language, CountryCode = "KE", IsActive = true };
    }

    private sealed class FakeMappingRepository(IReadOnlyCollection<ReservationLifecycleWhatsAppTemplateMapping> mappings) : IReservationLifecycleWhatsAppTemplateMappingRepository
    {
        public Task<ReservationLifecycleWhatsAppTemplateMapping?> GetEnabledMappingAsync(Guid companyId, Guid integrationId, ReservationLifecycleEventType eventType, string languageCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(mappings.FirstOrDefault(item => item.CompanyId == companyId
                && item.WhatsAppIntegrationId == integrationId
                && item.JourneyEventType == eventType
                && item.LanguageCode == languageCode
                && item.IsEnabled));
        }
    }
}
