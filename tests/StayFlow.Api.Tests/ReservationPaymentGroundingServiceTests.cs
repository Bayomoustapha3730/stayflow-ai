using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Tests;

/// <summary>
/// Verifies read-only payment grounding facts (totals, balance, latest status, receipt) and
/// that access is strictly tenant-scoped using the real EF-backed PaymentRepository.
/// </summary>
public sealed class ReservationPaymentGroundingServiceTests
{
    [Fact]
    public async Task GetReservationPaymentGroundingAsync_NoPayments_TotalPaidIsZero()
    {
        var (service, companyId, reservationId, _) = await SeedAsync(bookingAmount: 5000m);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result!.TotalPaid);
        Assert.False(result.HasSuccessfulPayment);
        Assert.Equal(0, result.PaymentCount);
        Assert.Null(result.LatestPaymentStatus);
        Assert.Null(result.LatestReceiptNumber);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_OnePaidPayment_IsCounted()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 5000m, PaymentStatus.Paid, receipt: "MPESA-1");

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(5000m, result!.TotalPaid);
        Assert.True(result.HasSuccessfulPayment);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_MultiplePaidPayments_AreSummed()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Paid, receipt: "MPESA-1", offsetMinutes: -10);
        await AddPaymentAsync(ctx, companyId, reservationId, 1500m, PaymentStatus.Paid, receipt: "MPESA-2", offsetMinutes: -5);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(3500m, result!.TotalPaid);
        Assert.Equal(2, result.PaymentCount);
    }

    [Theory]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Expired)]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Processing)]
    public async Task GetReservationPaymentGroundingAsync_NonPaidStatus_NotCountedTowardTotal(PaymentStatus status)
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, status);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(0m, result!.TotalPaid);
        Assert.False(result.HasSuccessfulPayment);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_PaidPlusFailed_OnlyCountsPaid()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Failed, offsetMinutes: -10);
        await AddPaymentAsync(ctx, companyId, reservationId, 3000m, PaymentStatus.Paid, receipt: "MPESA-1", offsetMinutes: -5);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(3000m, result!.TotalPaid);
        Assert.True(result.HasSuccessfulPayment);
        Assert.Equal(2, result.PaymentCount);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_RemainingBalance_IsBookingAmountMinusTotalPaid()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 3000m, PaymentStatus.Paid, receipt: "MPESA-1");

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(2000m, result!.RemainingBalance);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_RemainingBalance_NeverGoesBelowZero()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 1000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 1500m, PaymentStatus.Paid, receipt: "MPESA-1");

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(0m, result!.RemainingBalance);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_LatestPaymentStatus_ReflectsMostRecentAttempt()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Failed, offsetMinutes: -10);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Paid, receipt: "MPESA-1", offsetMinutes: -1);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(PaymentStatus.Paid.ToStorageValue(), result!.LatestPaymentStatus);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_ReceiptComesOnlyFromSuccessfulPaidPayment()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Failed);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Null(result!.LatestReceiptNumber);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_EarlierPaidReceipt_RemainsAvailableWhenLatestFailed()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Paid, receipt: "MPESA-1", offsetMinutes: -10);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Failed, offsetMinutes: -1);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal("MPESA-1", result!.LatestReceiptNumber);
        Assert.Equal(PaymentStatus.Failed.ToStorageValue(), result.LatestPaymentStatus);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Processing)]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Expired)]
    public async Task GetReservationPaymentGroundingAsync_LatestPaymentStatus_MatchesSingleAttemptStatus(PaymentStatus status)
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, status, receipt: status == PaymentStatus.Paid ? "MPESA-1" : null);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(status.ToStorageValue(), result!.LatestPaymentStatus);
        Assert.Equal(status == PaymentStatus.Paid, result.HasSuccessfulPayment);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_FailureMessage_OnlyPopulatedWhenLatestStatusIsFailed()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Failed, failureMessage: "Insufficient funds");

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal("Insufficient funds", result!.LatestFailureMessage);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_FailureMessage_NotPopulatedWhenLatestStatusIsCancelled()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Failed, failureMessage: "Insufficient funds", offsetMinutes: -10);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Cancelled, offsetMinutes: -1);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Null(result!.LatestFailureMessage);
        Assert.Equal(PaymentStatus.Cancelled.ToStorageValue(), result.LatestPaymentStatus);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_HasSuccessfulPayment_RemainsTrueWhenLatestAttemptFails()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 5000m, PaymentStatus.Paid, receipt: "MPESA-1", offsetMinutes: -10);
        await AddPaymentAsync(ctx, companyId, reservationId, 2000m, PaymentStatus.Failed, offsetMinutes: -1);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.True(result!.HasSuccessfulPayment);
        Assert.Equal(PaymentStatus.Failed.ToStorageValue(), result.LatestPaymentStatus);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_LatestPaymentDetails_ReflectMostRecentAttemptRegardlessOfStatus()
    {
        var (service, companyId, reservationId, ctx) = await SeedAsync(bookingAmount: 5000m);
        await AddPaymentAsync(ctx, companyId, reservationId, 5000m, PaymentStatus.Paid, receipt: "MPESA-1", offsetMinutes: -10);
        await AddPaymentAsync(ctx, companyId, reservationId, 1234m, PaymentStatus.Expired, offsetMinutes: -1);

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, companyId, CancellationToken.None);

        Assert.Equal(1234m, result!.LatestPaymentAmount);
        Assert.Equal("M-PESA", result.LatestProvider);
        Assert.Equal("STKPush", result.LatestPaymentMethod);
        Assert.Null(result.LatestPaymentCompletedAtUtc);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_CrossTenantReservation_ReturnsNull()
    {
        var (service, _, reservationId, _) = await SeedAsync(bookingAmount: 5000m);
        var otherCompanyId = Guid.NewGuid();

        var result = await service.GetReservationPaymentGroundingAsync(reservationId, otherCompanyId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetReservationPaymentGroundingAsync_UnknownReservation_ReturnsNull()
    {
        var (service, companyId, _, _) = await SeedAsync(bookingAmount: 5000m);

        var result = await service.GetReservationPaymentGroundingAsync(Guid.NewGuid(), companyId, CancellationToken.None);

        Assert.Null(result);
    }

    private static async Task<(ReservationPaymentGroundingService Service, Guid CompanyId, Guid ReservationId, ApplicationDbContext DbContext)> SeedAsync(decimal? bookingAmount)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"payment-grounding-{Guid.NewGuid():N}")
            .Options;

        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var dbContext = new ApplicationDbContext(options);

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Test Co",
            Slug = "test-co",
            NormalizedSlug = "TEST-CO",
            Status = "Active",
            Email = "test-co@stayflow.test",
            PhoneNumber = "+254700000001",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });

        dbContext.Properties.Add(new Property
        {
            Id = propertyId,
            CompanyId = companyId,
            Name = "Demo Property",
            City = "Nairobi",
            CountryCode = "KE",
            AddressLine1 = "1 Demo Street",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });

        dbContext.Guests.Add(new Guest
        {
            Id = guestId,
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@stayflow.test",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        });

        dbContext.Reservations.Add(new Reservation
        {
            Id = reservationId,
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ConfirmationNumber = "CONF-1",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
            Status = ReservationStatus.Confirmed,
            Currency = "KES",
            BookingAmount = bookingAmount,
            IsActive = true
        });

        await dbContext.SaveChangesAsync();

        var repository = new PaymentRepository(dbContext);
        var service = new ReservationPaymentGroundingService(repository, NullLogger<ReservationPaymentGroundingService>.Instance);

        return (service, companyId, reservationId, dbContext);
    }

    private static async Task AddPaymentAsync(
        ApplicationDbContext dbContext,
        Guid companyId,
        Guid reservationId,
        decimal amount,
        PaymentStatus status,
        string? receipt = null,
        double offsetMinutes = 0,
        string? failureMessage = null)
    {
        var reservation = await dbContext.Reservations.FirstAsync(r => r.Id == reservationId);
        var requestedAt = DateTimeOffset.UtcNow.AddMinutes(offsetMinutes);

        dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = reservation.PropertyId,
            GuestId = reservation.PrimaryGuestId,
            ReservationId = reservationId,
            Amount = amount,
            Currency = "KES",
            Provider = "M-PESA",
            PaymentMethod = "STKPush",
            Status = status.ToStorageValue(),
            ProviderTransactionId = status == PaymentStatus.Paid ? receipt : null,
            FailureMessage = status == PaymentStatus.Failed ? failureMessage : null,
            RequestedAtUtc = requestedAt,
            CompletedAtUtc = status == PaymentStatus.Paid ? requestedAt : null,
            FailedAtUtc = status == PaymentStatus.Failed ? requestedAt : null,
            CancelledAtUtc = status == PaymentStatus.Cancelled ? requestedAt : null
        });

        await dbContext.SaveChangesAsync();
    }
}
