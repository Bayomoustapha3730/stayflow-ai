using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Tests;

public sealed class MpesaPaymentFoundationTests
{
    [Theory]
    [InlineData("0712345678", "254712345678")]
    [InlineData("+254712345678", "254712345678")]
    [InlineData("254712345678", "254712345678")]
    public void KenyanPhoneNumberNormalizer_AcceptsSupportedFormats(string input, string expected)
    {
        var normalizer = new KenyanPhoneNumberNormalizer();

        Assert.True(normalizer.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("071234567")]
    [InlineData("0612345678")]
    [InlineData("254812345678")]
    [InlineData("not-a-phone")]
    public void KenyanPhoneNumberNormalizer_RejectsInvalidNumbers(string input)
    {
        var normalizer = new KenyanPhoneNumberNormalizer();

        Assert.False(normalizer.TryNormalize(input, out _));
    }

    [Fact]
    public async Task Callback_SuccessIsIdempotent_AndPaidDoesNotRegress()
    {
        var companyId = Guid.NewGuid();
        var payment = CreatePayment(companyId, PaymentStatus.Processing.ToStorageValue());
        var repository = new FakePaymentRepository(payment);
        var service = CreateService(repository, companyId);
        var callback = Callback(payment.ProviderCheckoutRequestId!, 0, "Receipt-123");

        Assert.Equal(MpesaCallbackResult.Processed, await service.HandleMpesaCallbackAsync(callback, CancellationToken.None));
        Assert.Equal(PaymentStatus.Paid.ToStorageValue(), payment.Status);
        Assert.Equal("Receipt-123", payment.ProviderTransactionId);

        Assert.Equal(MpesaCallbackResult.DuplicateIgnored, await service.HandleMpesaCallbackAsync(callback, CancellationToken.None));
        Assert.Equal(PaymentStatus.Paid.ToStorageValue(), payment.Status);

        var failure = Callback(payment.ProviderCheckoutRequestId!, 1032, null);
        Assert.Equal(MpesaCallbackResult.DuplicateIgnored, await service.HandleMpesaCallbackAsync(failure, CancellationToken.None));
        Assert.Equal(PaymentStatus.Paid.ToStorageValue(), payment.Status);
    }

    [Fact]
    public async Task Callback_UnknownCheckoutRequestIsIgnored()
    {
        var service = CreateService(new FakePaymentRepository(null), Guid.NewGuid());

        var result = await service.HandleMpesaCallbackAsync(Callback("unknown", 0, "Receipt-123"), CancellationToken.None);

        Assert.Equal(MpesaCallbackResult.UnknownCheckoutRequestIgnored, result);
    }

    [Fact]
    public async Task Callback_WithMismatchedMerchantRequestId_IsIgnored()
    {
        var payment = CreatePayment(Guid.NewGuid(), PaymentStatus.Pending.ToStorageValue());
        var service = CreateService(new FakePaymentRepository(payment), payment.CompanyId);

        var result = await service.HandleMpesaCallbackAsync(
            Callback(payment.ProviderCheckoutRequestId!, 0, "Receipt-123", "forged-merchant"),
            CancellationToken.None);

        Assert.Equal(MpesaCallbackResult.UnknownCheckoutRequestIgnored, result);
        Assert.Equal(PaymentStatus.Pending.ToStorageValue(), payment.Status);
    }

    [Fact]
    public async Task Callback_FailedAfterPaidIsIgnored()
    {
        var payment = CreatePayment(Guid.NewGuid(), PaymentStatus.Paid.ToStorageValue());
        var service = CreateService(new FakePaymentRepository(payment), payment.CompanyId);

        var result = await service.HandleMpesaCallbackAsync(Callback(payment.ProviderCheckoutRequestId!, 1032, null), CancellationToken.None);

        Assert.Equal(MpesaCallbackResult.DuplicateIgnored, result);
        Assert.Equal(PaymentStatus.Paid.ToStorageValue(), payment.Status);
    }

    [Fact]
    public async Task Callback_SuccessAfterFailedIsIgnored()
    {
        var payment = CreatePayment(Guid.NewGuid(), PaymentStatus.Failed.ToStorageValue());
        var service = CreateService(new FakePaymentRepository(payment), payment.CompanyId);

        var result = await service.HandleMpesaCallbackAsync(Callback(payment.ProviderCheckoutRequestId!, 0, "Receipt-456"), CancellationToken.None);

        Assert.Equal(MpesaCallbackResult.DuplicateIgnored, result);
        Assert.Equal(PaymentStatus.Failed.ToStorageValue(), payment.Status);
    }

    [Fact]
    public async Task Callback_SuccessWithoutReceiptMetadataIsIgnored()
    {
        var payment = CreatePayment(Guid.NewGuid(), PaymentStatus.Processing.ToStorageValue());
        var service = CreateService(new FakePaymentRepository(payment), payment.CompanyId);

        var result = await service.HandleMpesaCallbackAsync(Callback(payment.ProviderCheckoutRequestId!, 0, null), CancellationToken.None);

        Assert.Equal(MpesaCallbackResult.MalformedIgnored, result);
        Assert.Equal(PaymentStatus.Processing.ToStorageValue(), payment.Status);
    }

    [Fact]
    public async Task Initiate_UsesTheTenantReservationAmount()
    {
        var companyId = Guid.NewGuid();
        var reservation = CreateReservation(companyId, 2500m, "KES");
        var repository = new FakePaymentRepository(null, reservation);
        var service = CreateService(repository, companyId);

        var result = await service.InitiateMpesaPaymentAsync(new InitiateMpesaPaymentRequest
        {
            ReservationId = reservation.Id,
            CustomerPhoneNumber = "0712345678"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(repository.AddedPayment);
        Assert.Equal(2500m, repository.AddedPayment!.Amount);
        Assert.Equal("KES", repository.AddedPayment.Currency);
    }

    [Fact]
    public async Task Initiate_RejectsReservationWithoutAValidAmount()
    {
        var companyId = Guid.NewGuid();
        var reservation = CreateReservation(companyId, 0m, "KES");
        var repository = new FakePaymentRepository(null, reservation);
        var service = CreateService(repository, companyId);

        var result = await service.InitiateMpesaPaymentAsync(new InitiateMpesaPaymentRequest
        {
            ReservationId = reservation.Id,
            CustomerPhoneNumber = "0712345678"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.AddedPayment);
    }

    [Fact]
    public async Task Initiate_RejectsInvalidPhoneBeforeCreatingAPayment()
    {
        var companyId = Guid.NewGuid();
        var reservation = CreateReservation(companyId, 2500m, "KES");
        var repository = new FakePaymentRepository(null, reservation);
        var service = CreateService(repository, companyId);

        var result = await service.InitiateMpesaPaymentAsync(new InitiateMpesaPaymentRequest
        {
            ReservationId = reservation.Id,
            CustomerPhoneNumber = "0612345678"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.AddedPayment);
    }

    [Fact]
    public async Task GetPayment_DoesNotReturnAnotherCompanysPayment()
    {
        var payment = CreatePayment(Guid.NewGuid(), PaymentStatus.Pending.ToStorageValue());
        var service = CreateService(new FakePaymentRepository(payment), Guid.NewGuid());

        var result = await service.GetPaymentAsync(payment.Id, CancellationToken.None);

        Assert.False(result.Success);
    }

    private static PaymentService CreateService(FakePaymentRepository repository, Guid companyId)
    {
        return new PaymentService(
            repository,
            new TestTenantContext(companyId),
            new KenyanPhoneNumberNormalizer(),
            new FakeMpesaApiClient(),
            new FakeCredentialResolver(),
            Options.Create(new MpesaOptions { Enabled = true, ShortCode = "123456", CallbackBaseUrl = "https://example.test", DevelopmentMode = true }),
            NullLogger<PaymentService>.Instance);
    }

    private static Payment CreatePayment(Guid companyId, string status) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        PropertyId = Guid.NewGuid(),
        GuestId = Guid.NewGuid(),
        Amount = 100,
        Currency = "KES",
        ProviderRequestId = "merchant-123",
        ProviderCheckoutRequestId = "checkout-123",
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Reservation CreateReservation(Guid companyId, decimal amount, string currency) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        PropertyId = Guid.NewGuid(),
        PrimaryGuestId = Guid.NewGuid(),
        BookingAmount = amount,
        Currency = currency,
        Property = new Property { CompanyId = companyId },
        PrimaryGuest = new Guest { CompanyId = companyId }
    };

    private static string Callback(string checkoutRequestId, int resultCode, string? receipt, string merchantRequestId = "merchant-123")
    {
        var metadata = receipt is null ? "" : $",\"CallbackMetadata\":{{\"Item\":[{{\"Name\":\"MpesaReceiptNumber\",\"Value\":\"{receipt}\"}}]}}";
        return $"{{\"Body\":{{\"stkCallback\":{{\"MerchantRequestID\":\"{merchantRequestId}\",\"CheckoutRequestID\":\"{checkoutRequestId}\",\"ResultCode\":{resultCode},\"ResultDesc\":\"result\"{metadata}}}}}}}";
    }

    private sealed class TestTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId => companyId;
        public Guid? UserId => Guid.NewGuid();
        public string? CorrelationId => "test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeCredentialResolver : IMpesaCredentialResolver
    {
        public Task<MpesaCredentialResolution> ResolveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new MpesaCredentialResolution { Success = true, ConsumerKey = "key", ConsumerSecret = "secret", PassKey = "pass" });
    }

    private sealed class FakeMpesaApiClient : IMpesaApiClient
    {
        public Task<MpesaStkPushResponse> InitiateStkPushAsync(MpesaStkPushRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new MpesaStkPushResponse("merchant", "checkout", 0, "accepted", "accepted"));
    }

    private sealed class FakePaymentRepository(Payment? payment, Reservation? reservation = null) : IPaymentRepository
    {
        private readonly List<PaymentWebhookEvent> events = [];
        public Payment? AddedPayment { get; private set; }
        public Task<Payment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken) => Task.FromResult(payment?.Id == id && payment.CompanyId == companyId ? payment : null);
        public Task<IReadOnlyCollection<Payment>> GetByReservationIdAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Payment>>(payment is not null && payment.ReservationId == reservationId && payment.CompanyId == companyId ? [payment] : []);
        public Task<Payment?> GetByCheckoutRequestIdAsync(string checkoutRequestId, CancellationToken cancellationToken) => Task.FromResult(payment?.ProviderCheckoutRequestId == checkoutRequestId ? payment : null);
        public Task<Reservation?> GetReservationForPaymentAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken) => Task.FromResult(reservation?.Id == reservationId && reservation.CompanyId == companyId ? reservation : null);
        public Task<Payment?> GetByExternalReferenceAsync(string externalReference, Guid companyId, CancellationToken cancellationToken) => Task.FromResult<Payment?>(null);
        public Task<bool> ReservationBelongsToCompanyAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            AddedPayment = payment;
            return Task.CompletedTask;
        }
        public Task<bool> TryRecordWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken)
        {
            if (events.Any(item => item.Provider == webhookEvent.Provider && item.EventId == webhookEvent.EventId)) return Task.FromResult(false);
            events.Add(webhookEvent);
            return Task.FromResult(true);
        }
        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
