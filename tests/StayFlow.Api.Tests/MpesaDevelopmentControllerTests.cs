using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.Controllers;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Tests;

public sealed class MpesaDevelopmentControllerTests
{
    [Fact]
    public async Task Development_PendingPayment_SimulatesSuccessfulPayment()
    {
        var payment = CreatePayment(PaymentStatus.Pending);

        var result = await CreateController(payment).SimulateSuccess(payment.Id, CancellationToken.None);

        AssertSuccessfulSimulation(result, payment);
    }

    [Fact]
    public async Task Development_ProcessingPayment_SimulatesSuccessfulPayment()
    {
        var payment = CreatePayment(PaymentStatus.Processing);

        var result = await CreateController(payment).SimulateSuccess(payment.Id, CancellationToken.None);

        AssertSuccessfulSimulation(result, payment);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Expired)]
    public async Task TerminalPayment_ReturnsConflict_AndIsNotModified(PaymentStatus status)
    {
        var payment = CreatePayment(status);
        payment.ProviderTransactionId = "existing-receipt";
        payment.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        payment.FailureCode = status == PaymentStatus.Failed ? "1032" : null;
        payment.FailureMessage = status == PaymentStatus.Failed ? "existing failure" : null;
        payment.FailedAtUtc = status == PaymentStatus.Failed ? DateTimeOffset.UtcNow.AddMinutes(-5) : null;
        payment.CancelledAtUtc = status == PaymentStatus.Cancelled ? DateTimeOffset.UtcNow.AddMinutes(-5) : null;

        var before = Snapshot(payment);
        var result = await CreateController(payment).SimulateSuccess(payment.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(before, Snapshot(payment));
    }

    [Fact]
    public async Task UnknownPayment_ReturnsNotFound()
    {
        var controller = CreateController(null);

        var result = await controller.SimulateSuccess(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MissingPaymentData_ReturnsBadRequest(bool missingIdentifiers, bool missingPhone)
    {
        var payment = CreatePayment(PaymentStatus.Pending);
        if (missingIdentifiers)
        {
            payment.ProviderRequestId = null;
            payment.ProviderCheckoutRequestId = null;
        }

        if (missingPhone)
        {
            payment.CustomerPhoneNumber = null;
        }

        var result = await CreateController(payment).SimulateSuccess(payment.Id, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task NonDevelopmentEnvironment_ReturnsNotFound()
    {
        var payment = CreatePayment(PaymentStatus.Pending);
        var controller = CreateController(payment, "Production");

        var result = await controller.SimulateSuccess(payment.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(PaymentStatus.Pending.ToStorageValue(), payment.Status);
    }

    private static void AssertSuccessfulSimulation(IActionResult result, Payment payment)
    {
        var response = Assert.IsType<OkObjectResult>(result).Value;
        var responseType = response!.GetType();

        Assert.Equal(payment.Id, responseType.GetProperty("PaymentId")!.GetValue(response));
        Assert.Equal(PaymentStatus.Paid.ToStorageValue(), responseType.GetProperty("Status")!.GetValue(response));

        var receipt = Assert.IsType<string>(responseType.GetProperty("ProviderTransactionId")!.GetValue(response));
        Assert.StartsWith("STAYFLOWDEV", receipt);
        Assert.NotNull(responseType.GetProperty("CompletedAtUtc")!.GetValue(response));
        Assert.Null(responseType.GetProperty("FailureCode")!.GetValue(response));
        Assert.Null(responseType.GetProperty("FailureMessage")!.GetValue(response));
        Assert.Equal(PaymentStatus.Paid.ToStorageValue(), payment.Status);
        Assert.Equal(receipt, payment.ProviderTransactionId);
        Assert.NotNull(payment.CompletedAtUtc);
        Assert.Null(payment.FailureCode);
        Assert.Null(payment.FailureMessage);
        Assert.Null(payment.FailedAtUtc);
        Assert.Null(payment.CancelledAtUtc);
    }

    private static MpesaDevelopmentController CreateController(Payment? payment, string environmentName = "Development")
    {
        var repository = new FakePaymentRepository(payment);
        var service = new PaymentService(
            repository,
            new TestTenantContext(payment?.CompanyId ?? Guid.NewGuid()),
            new KenyanPhoneNumberNormalizer(),
            new FakeMpesaApiClient(),
            new FakeCredentialResolver(),
            Options.Create(new MpesaOptions
            {
                Enabled = true,
                ShortCode = "123456",
                CallbackBaseUrl = "https://example.test",
                DevelopmentMode = true
            }),
            new NoOpReservationPaymentGroundingService(),
            new NoOpPostPaymentNotificationService(),
            NullLogger<PaymentService>.Instance);

        return new MpesaDevelopmentController(
            new FakeHostEnvironment(environmentName),
            repository,
            service,
            NullLogger<MpesaDevelopmentController>.Instance);
    }

    private static Payment CreatePayment(PaymentStatus status) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        GuestId = Guid.NewGuid(),
        Amount = 100,
        Currency = "KES",
        ProviderRequestId = $"merchant-{Guid.NewGuid():N}",
        ProviderCheckoutRequestId = $"checkout-{Guid.NewGuid():N}",
        CustomerPhoneNumber = "254712345678",
        Status = status.ToStorageValue(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static PaymentSnapshot Snapshot(Payment payment) => new(
        payment.Status,
        payment.ProviderTransactionId,
        payment.CompletedAtUtc,
        payment.FailureCode,
        payment.FailureMessage,
        payment.FailedAtUtc,
        payment.CancelledAtUtc);

    private sealed record PaymentSnapshot(
        string Status,
        string? ProviderTransactionId,
        DateTimeOffset? CompletedAtUtc,
        string? FailureCode,
        string? FailureMessage,
        DateTimeOffset? FailedAtUtc,
        DateTimeOffset? CancelledAtUtc);

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "StayFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
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

        public Task<MpesaStkQueryResponse> QueryStkPushAsync(MpesaStkQueryRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new MpesaStkQueryResponse(0, "accepted", "merchant", request.CheckoutRequestId, null, null));
    }

    private sealed class FakePaymentRepository(Payment? payment) : IPaymentRepository
    {
        private readonly List<PaymentWebhookEvent> events = [];

        public Task<Payment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken) => Task.FromResult<Payment?>(null);
        public Task<Payment?> GetByIdWithoutTenantScopeAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(payment?.Id == id ? payment : null);
        public Task<IReadOnlyCollection<Payment>> GetByReservationIdAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Payment>>([]);
        public Task<Payment?> GetByCheckoutRequestIdAsync(string checkoutRequestId, CancellationToken cancellationToken) => Task.FromResult(payment?.ProviderCheckoutRequestId == checkoutRequestId ? payment : null);
        public Task<IReadOnlyCollection<Payment>> GetStaleMpesaPaymentsAsync(DateTimeOffset requestedBeforeUtc, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Payment>>([]);
        public Task<Reservation?> GetReservationForPaymentAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken) => Task.FromResult<Reservation?>(null);
        public Task<Payment?> GetByExternalReferenceAsync(string externalReference, Guid companyId, CancellationToken cancellationToken) => Task.FromResult<Payment?>(null);
        public Task<bool> ReservationBelongsToCompanyAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(Payment payment, CancellationToken cancellationToken) => Task.CompletedTask;
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