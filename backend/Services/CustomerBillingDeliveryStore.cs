using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class CustomerBillingDeliveryStore(ApplicationDbContext dbContext)
    : ICustomerBillingDeliveryStore
{
    public Task<CustomerBillableUsageOperation?> GetBillingOperationAsync(
        Guid billingOperationId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerBillableUsageOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == billingOperationId, cancellationToken);

    public async Task<CustomerBillingDelivery> PrepareAsync(Guid billingOperationId, DateTimeOffset preparedAtUtc)
    {
        ValidateUtc(preparedAtUtc, nameof(preparedAtUtc));
        var existing = await GetByBillingOperationAsync(billingOperationId, CancellationToken.None);
        if (existing is not null)
        {
            return existing;
        }

        var operation = await dbContext.CustomerBillableUsageOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == billingOperationId, CancellationToken.None)
            ?? throw new KeyNotFoundException($"Customer billing operation '{billingOperationId}' was not found.");

        if (!IsExternallyBillable(operation))
        {
            throw new InvalidOperationException("The customer billing operation is not eligible for external delivery.");
        }

        var isZeroCharge = operation.ChargeAmount == 0m;
        var delivery = CustomerBillingDelivery.Create(
            operation,
            isZeroCharge ? CustomerBillingDeliveryState.Suppressed : CustomerBillingDeliveryState.Prepared,
            isZeroCharge
                ? CustomerBillingDeliveryReason.ZeroChargeSuppressed
                : CustomerBillingDeliveryReason.Prepared,
            preparedAtUtc);
        dbContext.CustomerBillingDeliveries.Add(delivery);

        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return delivery;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(delivery).State = EntityState.Detached;
            var concurrentlyPrepared = await GetByBillingOperationAsync(
                billingOperationId,
                CancellationToken.None);
            if (concurrentlyPrepared is not null)
            {
                return concurrentlyPrepared;
            }

            throw;
        }
    }

    public Task<CustomerBillingDelivery?> GetByIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerBillingDeliveries
            .AsNoTracking()
            .SingleOrDefaultAsync(delivery => delivery.Id == deliveryId, cancellationToken);

    public Task<CustomerBillingDelivery?> GetByBillingOperationAsync(
        Guid billingOperationId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerBillingDeliveries
            .AsNoTracking()
            .SingleOrDefaultAsync(delivery =>
                delivery.CustomerBillableUsageOperationId == billingOperationId
                && delivery.DeliveryProvider == CustomerBillingDeliveryProvider.Stripe
                && delivery.DeliveryKind == CustomerBillingDeliveryKind.PendingExternalBilling,
                cancellationToken);

    public async Task<bool> TryStartDispatchAsync(Guid deliveryId, DateTimeOffset occurredAtUtc)
    {
        ValidateUtc(occurredAtUtc, nameof(occurredAtUtc));
        var changed = await dbContext.CustomerBillingDeliveries
            .Where(delivery => delivery.Id == deliveryId
                && (delivery.DeliveryState == CustomerBillingDeliveryState.Prepared
                    || (delivery.DeliveryState == CustomerBillingDeliveryState.Failed
                        && delivery.LastFailureCategory != null
                        && RetryableFailureCategories.Contains(delivery.LastFailureCategory.Value))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.DeliveryState, CustomerBillingDeliveryState.Dispatching)
                .SetProperty(delivery => delivery.DeliveryReason, CustomerBillingDeliveryReason.DispatchClaimed)
                .SetProperty(delivery => delivery.AttemptCount, delivery => delivery.AttemptCount + 1)
                .SetProperty(delivery => delivery.FirstAttemptAtUtc,
                    delivery => delivery.FirstAttemptAtUtc ?? occurredAtUtc)
                .SetProperty(delivery => delivery.LastAttemptAtUtc, occurredAtUtc)
                .SetProperty(delivery => delivery.UpdatedAtUtc, occurredAtUtc));
        return changed == 1;
    }

    public async Task<bool> RecordDeliveredAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc)
    {
        ValidateUtc(occurredAtUtc, nameof(occurredAtUtc));
        ValidateOptionalIdentifier(externalObjectId, nameof(externalObjectId));
        ValidateOptionalIdentifier(externalRequestId, nameof(externalRequestId));
        var changed = await dbContext.CustomerBillingDeliveries
            .Where(delivery => delivery.Id == deliveryId
                && delivery.DeliveryState == CustomerBillingDeliveryState.Dispatching)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.DeliveryState, CustomerBillingDeliveryState.Delivered)
                .SetProperty(delivery => delivery.DeliveryReason, CustomerBillingDeliveryReason.Delivered)
                .SetProperty(delivery => delivery.ExternalObjectId, externalObjectId)
                .SetProperty(delivery => delivery.ExternalRequestId, externalRequestId)
                .SetProperty(delivery => delivery.DeliveredAtUtc, occurredAtUtc)
                .SetProperty(delivery => delivery.LastFailureCategory, (CustomerBillingDeliveryFailureCategory?)null)
                .SetProperty(delivery => delivery.LastFailureCode, (string?)null)
                .SetProperty(delivery => delivery.UpdatedAtUtc, occurredAtUtc), CancellationToken.None);
        return changed == 1;
    }

    public async Task<bool> RecordFailedAsync(
        Guid deliveryId,
        CustomerBillingDeliveryFailureCategory failureCategory,
        string failureCode,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc)
    {
        ValidateUtc(occurredAtUtc, nameof(occurredAtUtc));
        ValidateFailureCode(failureCode);
        ValidateOptionalIdentifier(externalRequestId, nameof(externalRequestId));
        if (!Enum.IsDefined(failureCategory))
        {
            throw new ArgumentOutOfRangeException(nameof(failureCategory));
        }

        var changed = await dbContext.CustomerBillingDeliveries
            .Where(delivery => delivery.Id == deliveryId
                && delivery.DeliveryState == CustomerBillingDeliveryState.Dispatching)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.DeliveryState, CustomerBillingDeliveryState.Failed)
                .SetProperty(delivery => delivery.DeliveryReason, ToReason(failureCategory))
                .SetProperty(delivery => delivery.ExternalRequestId, externalRequestId)
                .SetProperty(delivery => delivery.LastFailureCategory, failureCategory)
                .SetProperty(delivery => delivery.LastFailureCode, failureCode)
                .SetProperty(delivery => delivery.UpdatedAtUtc, occurredAtUtc), CancellationToken.None);
        return changed == 1;
    }

    public async Task<bool> RecordIndeterminateAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId,
        string? failureCode,
        DateTimeOffset occurredAtUtc)
    {
        ValidateUtc(occurredAtUtc, nameof(occurredAtUtc));
        ValidateOptionalIdentifier(externalObjectId, nameof(externalObjectId));
        ValidateOptionalIdentifier(externalRequestId, nameof(externalRequestId));
        if (failureCode is not null)
        {
            ValidateFailureCode(failureCode);
        }

        var changed = await dbContext.CustomerBillingDeliveries
            .Where(delivery => delivery.Id == deliveryId
                && delivery.DeliveryState == CustomerBillingDeliveryState.Dispatching)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.DeliveryState, CustomerBillingDeliveryState.Indeterminate)
                .SetProperty(delivery => delivery.DeliveryReason, CustomerBillingDeliveryReason.IndeterminateExternalOutcome)
                .SetProperty(delivery => delivery.ExternalObjectId, externalObjectId)
                .SetProperty(delivery => delivery.ExternalRequestId, externalRequestId)
                .SetProperty(delivery => delivery.IndeterminateAtUtc, occurredAtUtc)
                .SetProperty(delivery => delivery.LastFailureCategory, (CustomerBillingDeliveryFailureCategory?)null)
                .SetProperty(delivery => delivery.LastFailureCode, failureCode)
                .SetProperty(delivery => delivery.UpdatedAtUtc, occurredAtUtc), CancellationToken.None);
        return changed == 1;
    }

    public async Task<bool> RecordReconciledDeliveredAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc)
    {
        ValidateUtc(occurredAtUtc, nameof(occurredAtUtc));
        ValidateOptionalIdentifier(externalObjectId, nameof(externalObjectId));
        ValidateOptionalIdentifier(externalRequestId, nameof(externalRequestId));
        var changed = await dbContext.CustomerBillingDeliveries
            .Where(delivery => delivery.Id == deliveryId
                && delivery.DeliveryState == CustomerBillingDeliveryState.Indeterminate)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.DeliveryState, CustomerBillingDeliveryState.Delivered)
                .SetProperty(delivery => delivery.DeliveryReason, CustomerBillingDeliveryReason.Delivered)
                .SetProperty(delivery => delivery.ExternalObjectId, externalObjectId)
                .SetProperty(delivery => delivery.ExternalRequestId, externalRequestId)
                .SetProperty(delivery => delivery.DeliveredAtUtc, occurredAtUtc)
                .SetProperty(delivery => delivery.LastFailureCategory, (CustomerBillingDeliveryFailureCategory?)null)
                .SetProperty(delivery => delivery.LastFailureCode, (string?)null)
                .SetProperty(delivery => delivery.UpdatedAtUtc, occurredAtUtc), CancellationToken.None);
        if (changed == 1)
        {
            return true;
        }

        var current = await GetByIdAsync(deliveryId, CancellationToken.None);
        return current?.DeliveryState == CustomerBillingDeliveryState.Delivered
            && current.ExternalObjectId == externalObjectId
            && current.ExternalRequestId == externalRequestId;
    }

    public async Task<bool> RecordReconciledRejectedAsync(
        Guid deliveryId,
        string failureCode,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc)
    {
        ValidateUtc(occurredAtUtc, nameof(occurredAtUtc));
        ValidateFailureCode(failureCode);
        ValidateOptionalIdentifier(externalRequestId, nameof(externalRequestId));
        var changed = await dbContext.CustomerBillingDeliveries
            .Where(delivery => delivery.Id == deliveryId
                && delivery.DeliveryState == CustomerBillingDeliveryState.Indeterminate)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.DeliveryState, CustomerBillingDeliveryState.Failed)
                .SetProperty(delivery => delivery.DeliveryReason, CustomerBillingDeliveryReason.ProviderRejected)
                .SetProperty(delivery => delivery.ExternalRequestId, externalRequestId)
                .SetProperty(delivery => delivery.LastFailureCategory, CustomerBillingDeliveryFailureCategory.ProviderRejected)
                .SetProperty(delivery => delivery.LastFailureCode, failureCode)
                .SetProperty(delivery => delivery.UpdatedAtUtc, occurredAtUtc), CancellationToken.None);
        if (changed == 1)
        {
            return true;
        }

        var current = await GetByIdAsync(deliveryId, CancellationToken.None);
        return current?.DeliveryState == CustomerBillingDeliveryState.Failed
            && current.LastFailureCategory == CustomerBillingDeliveryFailureCategory.ProviderRejected
            && current.LastFailureCode == failureCode
            && current.ExternalRequestId == externalRequestId;
    }

    private static bool IsExternallyBillable(CustomerBillableUsageOperation operation) =>
        operation.BillingState == CustomerBillableUsageState.Calculated
        && operation.ChargeAmount is not null
        && operation.Currency == "USD"
        && operation.CustomerUsagePriceVersionId is not null
        && operation.BillableQuantity >= 0;

    private static readonly CustomerBillingDeliveryFailureCategory[] RetryableFailureCategories =
    [
        CustomerBillingDeliveryFailureCategory.RetryableProviderUnavailable
    ];

    private static CustomerBillingDeliveryReason ToReason(CustomerBillingDeliveryFailureCategory category) =>
        category switch
        {
            CustomerBillingDeliveryFailureCategory.ProviderRejected => CustomerBillingDeliveryReason.ProviderRejected,
            CustomerBillingDeliveryFailureCategory.ProviderUnavailable => CustomerBillingDeliveryReason.ProviderUnavailable,
            CustomerBillingDeliveryFailureCategory.Timeout => CustomerBillingDeliveryReason.Timeout,
            CustomerBillingDeliveryFailureCategory.Canceled => CustomerBillingDeliveryReason.Canceled,
            CustomerBillingDeliveryFailureCategory.NetworkFailure => CustomerBillingDeliveryReason.NetworkFailure,
            CustomerBillingDeliveryFailureCategory.PersistenceFailure => CustomerBillingDeliveryReason.PersistenceFailure,
            CustomerBillingDeliveryFailureCategory.ConfigurationMissing => CustomerBillingDeliveryReason.ConfigurationMissing,
            CustomerBillingDeliveryFailureCategory.InvalidSubscriptionMapping => CustomerBillingDeliveryReason.InvalidSubscriptionMapping,
            CustomerBillingDeliveryFailureCategory.InvalidPriceMapping => CustomerBillingDeliveryReason.InvalidPriceMapping,
            CustomerBillingDeliveryFailureCategory.RetryableProviderUnavailable => CustomerBillingDeliveryReason.ProviderUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

    private static void ValidateUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamps must use UTC.", parameterName);
        }
    }

    private static void ValidateFailureCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 80
            || value.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.' and not ':'))
        {
            throw new ArgumentException(
                "Failure codes must contain 1 to 80 ASCII letters, digits, or . _ : - characters.",
                nameof(value));
        }
    }

    private static void ValidateOptionalIdentifier(string? value, string parameterName)
    {
        if (value is { Length: > 200 } || value is { Length: 0 })
        {
            throw new ArgumentException("External identifiers must contain 1 to 200 characters.", parameterName);
        }
    }
}
