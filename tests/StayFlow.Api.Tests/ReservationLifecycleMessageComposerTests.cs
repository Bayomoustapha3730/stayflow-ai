using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationLifecycleMessageComposerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid GuestId = Guid.NewGuid();

    [Theory]
    [InlineData(ReservationLifecycleEventType.PreArrival)]
    [InlineData(ReservationLifecycleEventType.ArrivalDay)]
    [InlineData(ReservationLifecycleEventType.InStay)]
    [InlineData(ReservationLifecycleEventType.CheckoutDay)]
    [InlineData(ReservationLifecycleEventType.PostStay)]
    public void Compose_MentionsGuestAndPropertyAndDoesNotFabricateOperationalFacts(ReservationLifecycleEventType eventType)
    {
        var composer = new ReservationLifecycleMessageComposer();
        var reservation = NewReservation();
        var property = NewProperty();
        var guest = NewGuest("en");
        var lifecycleEvent = NewLifecycleEvent(eventType, reservation.CheckInDate);

        var spec = composer.Compose(lifecycleEvent, reservation, property, guest);

        Assert.Equal("en", spec.Language);
        Assert.Contains(guest.FirstName, spec.RenderedContent);
        Assert.Contains(property.Name, spec.RenderedContent);
        AssertNoFabricatedOperationalContent(spec.RenderedContent);
    }

    [Fact]
    public void Compose_PreArrival_IncludesCheckInDateOnly()
    {
        var composer = new ReservationLifecycleMessageComposer();
        var reservation = NewReservation(checkInDate: new DateOnly(2026, 12, 24));
        var lifecycleEvent = NewLifecycleEvent(ReservationLifecycleEventType.PreArrival, reservation.CheckInDate);

        var spec = composer.Compose(lifecycleEvent, reservation, NewProperty(), NewGuest("en"));

        Assert.Contains("December 24, 2026", spec.RenderedContent);
    }

    [Fact]
    public void Compose_WithSupportedFrenchLanguage_ComposesFrenchContent()
    {
        var composer = new ReservationLifecycleMessageComposer();
        var reservation = NewReservation();
        var lifecycleEvent = NewLifecycleEvent(ReservationLifecycleEventType.ArrivalDay, reservation.CheckInDate);

        var spec = composer.Compose(lifecycleEvent, reservation, NewProperty(), NewGuest("fr"));

        Assert.Equal("fr", spec.Language);
        Assert.Contains("Bonjour", spec.RenderedContent);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("")]
    [InlineData(null)]
    public void Compose_WithUnsupportedOrMissingLanguage_FallsBackToEnglish(string? preferredLanguage)
    {
        var composer = new ReservationLifecycleMessageComposer();
        var reservation = NewReservation();
        var lifecycleEvent = NewLifecycleEvent(ReservationLifecycleEventType.ArrivalDay, reservation.CheckInDate);

        var spec = composer.Compose(lifecycleEvent, reservation, NewProperty(), NewGuest(preferredLanguage));

        Assert.Equal("en", spec.Language);
        Assert.Contains("Hi", spec.RenderedContent);
    }

    [Fact]
    public void Compose_InStay_AnchorDoesNotMentionCheckInOrCheckoutDates()
    {
        var composer = new ReservationLifecycleMessageComposer();
        var reservation = NewReservation();
        var lifecycleEvent = NewLifecycleEvent(ReservationLifecycleEventType.InStay, reservation.CheckInDate.AddDays(1));

        var spec = composer.Compose(lifecycleEvent, reservation, NewProperty(), NewGuest("en"));

        Assert.DoesNotContain(reservation.CheckInDate.ToString("MMMM"), spec.RenderedContent);
    }

    private static void AssertNoFabricatedOperationalContent(string content)
    {
        var lowered = content.ToLowerInvariant();
        Assert.DoesNotContain("door code", lowered);
        Assert.DoesNotContain("wifi", lowered);
        Assert.DoesNotContain("wi-fi", lowered);
        Assert.DoesNotContain("parking", lowered);
        Assert.DoesNotContain("check-in time", lowered);
        Assert.DoesNotContain("checkout time", lowered);
        Assert.DoesNotContain("pm", lowered);
        Assert.DoesNotContain("am", lowered);
    }

    private static Reservation NewReservation(DateOnly? checkInDate = null)
    {
        var checkIn = checkInDate ?? new DateOnly(2026, 8, 10);
        return new Reservation
        {
            Id = ReservationId,
            CompanyId = CompanyId,
            PropertyId = PropertyId,
            PrimaryGuestId = GuestId,
            ReservationSource = "Manual",
            CheckInDate = checkIn,
            CheckOutDate = checkIn.AddDays(4),
            Adults = 2,
            Status = ReservationStatus.Confirmed,
            IsActive = true
        };
    }

    private static Property NewProperty()
    {
        return new Property
        {
            Id = PropertyId,
            CompanyId = CompanyId,
            Name = "Demo Property",
            AddressLine1 = "Road",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        };
    }

    private static Guest NewGuest(string? preferredLanguage)
    {
        return new Guest
        {
            Id = GuestId,
            CompanyId = CompanyId,
            FirstName = "Ada",
            LastName = "Guest",
            PreferredLanguage = preferredLanguage ?? "en",
            CountryCode = "KE",
            IsActive = true
        };
    }

    private static ReservationLifecycleEvent NewLifecycleEvent(ReservationLifecycleEventType eventType, DateOnly propertyLocalDate)
    {
        return new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            ReservationId = ReservationId,
            PropertyId = PropertyId,
            GuestId = GuestId,
            EventType = eventType,
            RuleVersion = ReservationLifecycleRuleVersions.V1,
            PropertyLocalDate = propertyLocalDate,
            ScheduledForUtc = new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero),
            Status = ReservationLifecycleEventStatus.Pending,
            IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(CompanyId, ReservationId, eventType, propertyLocalDate, ReservationLifecycleRuleVersions.V1)
        };
    }
}
