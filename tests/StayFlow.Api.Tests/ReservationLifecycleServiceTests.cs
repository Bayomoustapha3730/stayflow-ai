using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StayFlow.Api.Extensions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationLifecycleServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid GuestId = Guid.NewGuid();

    [Fact]
    public void AddApplicationServices_ResolvesLifecycleDependencies()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ReservationContextOptions.SectionName}:PreArrivalWindowDays"] = "5"
            })
            .Build();

        services.AddApplicationServices(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TimeProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReservationLifecycleService>());
        Assert.Equal(5, scope.ServiceProvider.GetRequiredService<IOptions<ReservationContextOptions>>().Value.PreArrivalWindowDays);
    }

    [Fact]
    public void GetContext_WithDraftStatus_ReturnsNotConfirmed()
    {
        var context = BuildContext(ReservationStatus.Draft, new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 24));

        Assert.Equal(ReservationLifecycleStage.NotConfirmed, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithPendingConfirmationStatus_ReturnsNotConfirmed()
    {
        var context = BuildContext(ReservationStatus.PendingConfirmation, new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 24));

        Assert.Equal(ReservationLifecycleStage.NotConfirmed, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithCancelledStatus_ReturnsCancelled()
    {
        var context = BuildContext(ReservationStatus.Cancelled, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));

        Assert.Equal(ReservationLifecycleStage.Cancelled, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithNoShowStatus_ReturnsNoShow()
    {
        var context = BuildContext(ReservationStatus.NoShow, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));

        Assert.Equal(ReservationLifecycleStage.NoShow, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithFutureConfirmedReservationBeforePreArrivalWindow_ReturnsFutureConfirmed()
    {
        var context = BuildContext(ReservationStatus.Confirmed, new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 24));

        Assert.Equal(ReservationLifecycleStage.FutureConfirmed, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_OnPreArrivalWindowFirstBoundary_ReturnsPreArrival()
    {
        var context = BuildContext(ReservationStatus.Confirmed, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 20));

        Assert.Equal(ReservationLifecycleStage.PreArrival, context.LifecycleStage);
        Assert.Equal(7, context.DaysUntilCheckIn);
    }

    [Fact]
    public void GetContext_OneDayBeforeArrival_ReturnsPreArrival()
    {
        var context = BuildContext(ReservationStatus.Confirmed, new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 15));

        Assert.Equal(ReservationLifecycleStage.PreArrival, context.LifecycleStage);
        Assert.Equal(1, context.DaysUntilCheckIn);
    }

    [Fact]
    public void GetContext_OnLocalCheckInDate_ReturnsArrivingToday()
    {
        var context = BuildContext(ReservationStatus.Confirmed, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));

        Assert.Equal(ReservationLifecycleStage.ArrivingToday, context.LifecycleStage);
        Assert.True(context.IsCurrentlyInStay);
    }

    [Fact]
    public void GetContext_OnDayAfterCheckIn_ReturnsInStayWithoutRequiringCheckedInStatus()
    {
        var context = BuildContext(ReservationStatus.Confirmed, new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 14));

        Assert.Equal(ReservationLifecycleStage.InStay, context.LifecycleStage);
        Assert.True(context.IsCurrentlyInStay);
    }

    [Fact]
    public void GetContext_OnLocalCheckoutDate_ReturnsCheckingOutToday()
    {
        var context = BuildContext(ReservationStatus.Confirmed, new DateOnly(2026, 8, 6), new DateOnly(2026, 8, 10));

        Assert.Equal(ReservationLifecycleStage.CheckingOutToday, context.LifecycleStage);
        Assert.True(context.IsCurrentlyInStay);
    }

    [Fact]
    public void GetContext_OnDayAfterCheckout_ReturnsCompleted()
    {
        var context = BuildContext(ReservationStatus.Confirmed, new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 9));

        Assert.Equal(ReservationLifecycleStage.Completed, context.LifecycleStage);
        Assert.False(context.IsCurrentlyInStay);
    }

    [Fact]
    public void GetContext_WithCompletedStatus_ReturnsCompletedEvenBeforeCheckout()
    {
        var context = BuildContext(ReservationStatus.Completed, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));

        Assert.Equal(ReservationLifecycleStage.Completed, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithAfricaNairobiBoundary_UsesPropertyLocalDateWhenUtcDateDiffers()
    {
        var service = CreateService(new DateTimeOffset(2026, 8, 10, 21, 30, 0, TimeSpan.Zero));
        var context = service.GetContext(
            NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 14)),
            NewProperty("Africa/Nairobi"));

        Assert.Equal(new DateOnly(2026, 8, 11), DateOnly.FromDateTime(context.CurrentLocalDateTime.DateTime));
        Assert.Equal(ReservationLifecycleStage.ArrivingToday, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithTimezoneWestOfUtc_UsesPropertyLocalDateWhenUtcDateDiffers()
    {
        var service = CreateService(new DateTimeOffset(2026, 8, 11, 2, 30, 0, TimeSpan.Zero));
        var context = service.GetContext(
            NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 14)),
            NewProperty("America/Los_Angeles"));

        Assert.Equal(new DateOnly(2026, 8, 10), DateOnly.FromDateTime(context.CurrentLocalDateTime.DateTime));
        Assert.Equal(ReservationLifecycleStage.PreArrival, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithDstAwareTimezone_UsesTimezoneRulesForCurrentLocalDateTime()
    {
        var service = CreateService(new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero));
        var context = service.GetContext(
            NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10)),
            NewProperty("America/New_York"));

        Assert.Equal(new DateOnly(2026, 3, 8), DateOnly.FromDateTime(context.CurrentLocalDateTime.DateTime));
        Assert.Equal(TimeSpan.FromHours(-4), context.CurrentLocalDateTime.Offset);
        Assert.Equal(ReservationLifecycleStage.ArrivingToday, context.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithSameInstantAndDifferentPropertyTimezones_CanReturnDifferentStages()
    {
        var service = CreateService(new DateTimeOffset(2026, 8, 10, 21, 30, 0, TimeSpan.Zero));
        var reservation = NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 14));

        var nairobiContext = service.GetContext(reservation, NewProperty("Africa/Nairobi"));
        var losAngelesContext = service.GetContext(reservation, NewProperty("America/Los_Angeles"));

        Assert.Equal(ReservationLifecycleStage.ArrivingToday, nairobiContext.LifecycleStage);
        Assert.Equal(ReservationLifecycleStage.PreArrival, losAngelesContext.LifecycleStage);
    }

    [Fact]
    public void GetContext_UsesConfiguredPreArrivalWindowDays()
    {
        var service = CreateService(new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero), preArrivalWindowDays: 3);
        var property = NewProperty("Africa/Nairobi");

        var outsideWindowContext = service.GetContext(
            NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 17)),
            property);
        var boundaryContext = service.GetContext(
            NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 13), new DateOnly(2026, 8, 17)),
            property);

        Assert.Equal(ReservationLifecycleStage.FutureConfirmed, outsideWindowContext.LifecycleStage);
        Assert.Equal(ReservationLifecycleStage.PreArrival, boundaryContext.LifecycleStage);
        Assert.Equal(3, boundaryContext.PreArrivalWindowDays);
    }

    [Fact]
    public void GetContext_CancelledAndNoShowOverrideTemporalCalculation()
    {
        var cancelledContext = BuildContext(ReservationStatus.Cancelled, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));
        var noShowContext = BuildContext(ReservationStatus.NoShow, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));

        Assert.Equal(ReservationLifecycleStage.Cancelled, cancelledContext.LifecycleStage);
        Assert.Equal(ReservationLifecycleStage.NoShow, noShowContext.LifecycleStage);
    }

    [Fact]
    public void GetContext_WithSameGuestMultipleReservations_ReturnsIndependentContexts()
    {
        var service = CreateService(new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));
        var property = NewProperty("Africa/Nairobi");
        var activeReservation = NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));
        var futureReservation = NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 30), new DateOnly(2026, 9, 3));

        var activeContext = service.GetContext(activeReservation, property);
        var futureContext = service.GetContext(futureReservation, property);

        Assert.Equal(GuestId, activeContext.GuestId);
        Assert.Equal(GuestId, futureContext.GuestId);
        Assert.Equal(ReservationLifecycleStage.ArrivingToday, activeContext.LifecycleStage);
        Assert.Equal(ReservationLifecycleStage.FutureConfirmed, futureContext.LifecycleStage);
        Assert.NotEqual(activeContext.ReservationId, futureContext.ReservationId);
    }

    [Fact]
    public void GetContext_PreservesCompanyPropertyReservationAndGuestIdentifiers()
    {
        var reservation = NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));
        var property = NewProperty("Africa/Nairobi");

        var context = CreateService(new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero)).GetContext(reservation, property);

        Assert.Equal(reservation.Id, context.ReservationId);
        Assert.Equal(CompanyId, context.CompanyId);
        Assert.Equal(PropertyId, context.PropertyId);
        Assert.Equal(GuestId, context.GuestId);
        Assert.Equal("Africa/Nairobi", context.PropertyTimeZone);
        Assert.Equal(reservation.CheckInDate, context.CheckInLocal);
        Assert.Equal(reservation.CheckOutDate, context.CheckOutLocal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/AZone")]
    public void GetContext_WithMissingOrInvalidPropertyTimezone_ThrowsValidationFailure(string? timeZone)
    {
        var service = CreateService(new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));

        Assert.Throws<ArgumentException>(() => service.GetContext(
            NewReservation(ReservationStatus.Confirmed, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14)),
            NewProperty(timeZone!)));
    }

    private static ReservationLifecycleContext BuildContext(
        ReservationStatus status,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        string timeZone = "Africa/Nairobi",
        int preArrivalWindowDays = 7)
    {
        return CreateService(new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero), preArrivalWindowDays)
            .GetContext(NewReservation(status, checkInDate, checkOutDate), NewProperty(timeZone));
    }

    private static ReservationLifecycleService CreateService(DateTimeOffset utcNow, int preArrivalWindowDays = 7)
    {
        return new ReservationLifecycleService(
            new FrozenTimeProvider(utcNow),
            Options.Create(new ReservationContextOptions { PreArrivalWindowDays = preArrivalWindowDays }));
    }

    private static Reservation NewReservation(ReservationStatus status, DateOnly checkInDate, DateOnly checkOutDate)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            PropertyId = PropertyId,
            PrimaryGuestId = GuestId,
            ReservationSource = "Manual",
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            Adults = 2,
            Children = 0,
            TotalGuestCount = 2,
            Status = status,
            IsActive = true
        };
    }

    private static Property NewProperty(string timeZone)
    {
        return new Property
        {
            Id = PropertyId,
            CompanyId = CompanyId,
            Name = "Demo Property",
            AddressLine1 = "Road",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = timeZone,
            IsActive = true
        };
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}