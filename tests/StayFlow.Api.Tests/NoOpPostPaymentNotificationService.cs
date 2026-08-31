using StayFlow.Api.Models;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Tests;

/// <summary>Test double that records nothing and never throws, mirroring production failure isolation.</summary>
internal sealed class NoOpPostPaymentNotificationService : IPostPaymentNotificationService
{
    public Task NotifyPaymentPaidAsync(Payment payment, CancellationToken cancellationToken) => Task.CompletedTask;
}
