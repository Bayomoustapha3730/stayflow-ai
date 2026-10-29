using Microsoft.Extensions.Options;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Billing;

public sealed class CustomerBillingDeliveryDispatcher(
    ICustomerBillingDeliveryStore deliveryStore,
    ICustomerBillingExternalTransport transport,
    IOptions<CustomerUsageBillingOptions> usageBillingOptions,
    TimeProvider timeProvider,
    ILogger<CustomerBillingDeliveryDispatcher> logger) : ICustomerBillingDeliveryDispatcher
{
    public async Task<CustomerBillingDeliveryDispatcherResult> DispatchAsync(
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var delivery = await deliveryStore.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null)
        {
            return Missing(deliveryId);
        }

        if (delivery.DeliveryState == CustomerBillingDeliveryState.Suppressed)
        {
            return CreateResult(delivery, CustomerBillingDeliveryDispatcherOutcome.NotEligible, "delivery_suppressed");
        }

        var mappingResult = ResolveMapping(delivery);
        if (mappingResult.Mapping is null)
        {
            return CreateResult(
                delivery,
                CustomerBillingDeliveryDispatcherOutcome.ConfigurationError,
                mappingResult.ErrorCode!);
        }

        if (!HasValidBillingSnapshot(delivery))
        {
            return CreateResult(
                delivery,
                CustomerBillingDeliveryDispatcherOutcome.ConfigurationError,
                "delivery_snapshot_invalid");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await deliveryStore.TryStartDispatchAsync(deliveryId, timeProvider.GetUtcNow()))
        {
            var current = await deliveryStore.GetByIdAsync(deliveryId, cancellationToken);
            return current is null
                ? Missing(deliveryId)
                : CreateResult(current, CustomerBillingDeliveryDispatcherOutcome.NotEligible, "dispatch_claim_not_acquired");
        }

        var attemptCount = delivery.AttemptCount + 1;
        var claimedDelivery = await deliveryStore.GetByIdAsync(deliveryId, CancellationToken.None);
        if (claimedDelivery is null
            || claimedDelivery.DeliveryState != CustomerBillingDeliveryState.Dispatching
            || !HasValidBillingSnapshot(claimedDelivery))
        {
            logger.LogError("Claimed customer billing delivery could not be loaded as a valid dispatch snapshot. DeliveryId: {DeliveryId}", deliveryId);
            return CreateResult(
                claimedDelivery ?? delivery,
                CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                "claimed_delivery_unavailable",
                attemptCount);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            var canceled = await deliveryStore.RecordFailedAsync(
                deliveryId,
                CustomerBillingDeliveryFailureCategory.Canceled,
                "caller_canceled_before_send",
                null,
                timeProvider.GetUtcNow());
            if (!canceled)
            {
                logger.LogError("Canceled customer billing dispatch could not persist its pre-send failure. DeliveryId: {DeliveryId}", deliveryId);
                return CreateResult(
                    claimedDelivery,
                    CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                    "pre_send_cancellation_persistence_failed",
                    attemptCount,
                    state: CustomerBillingDeliveryState.Dispatching);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        var request = CustomerBillingExternalRequest.FromDelivery(claimedDelivery, mappingResult.Mapping);
        CustomerBillingExternalResult transportResult;
        try
        {
            transportResult = await transport.SendAsync(request, cancellationToken);
        }
        catch (Exception)
        {
            logger.LogWarning("Customer billing transport threw after dispatch began; recording an indeterminate outcome. DeliveryId: {DeliveryId}", deliveryId);
            var indeterminatePersisted = await TryRecordIndeterminateAsync(
                deliveryId,
                null,
                null,
                "transport_exception");
            return CreateResult(
                claimedDelivery,
                indeterminatePersisted
                    ? CustomerBillingDeliveryDispatcherOutcome.Indeterminate
                    : CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                indeterminatePersisted ? "transport_exception" : "outcome_persistence_failed",
                attemptCount,
                state: indeterminatePersisted ? CustomerBillingDeliveryState.Indeterminate : CustomerBillingDeliveryState.Dispatching);
        }

        if (transportResult is null || !Enum.IsDefined(transportResult.Outcome))
        {
            logger.LogWarning("Customer billing transport returned an invalid result; recording an indeterminate outcome. DeliveryId: {DeliveryId}", deliveryId);
            var invalidResultPersisted = await TryRecordIndeterminateAsync(
                deliveryId,
                null,
                null,
                "invalid_transport_result");
            return CreateResult(
                claimedDelivery,
                invalidResultPersisted
                    ? CustomerBillingDeliveryDispatcherOutcome.Indeterminate
                    : CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                invalidResultPersisted ? "invalid_transport_result" : "outcome_persistence_failed",
                attemptCount,
                state: invalidResultPersisted ? CustomerBillingDeliveryState.Indeterminate : CustomerBillingDeliveryState.Dispatching);
        }

        var providerCode = BoundedCode(transportResult.ReasonCode, "provider_outcome");
        var externalObjectId = BoundedIdentifier(transportResult.ExternalObjectId);
        var externalRequestId = BoundedIdentifier(transportResult.ExternalRequestId);
        var outcome = transportResult.Outcome switch
        {
            CustomerBillingExternalTransportOutcome.Delivered => CustomerBillingDeliveryDispatcherOutcome.Delivered,
            CustomerBillingExternalTransportOutcome.Rejected => CustomerBillingDeliveryDispatcherOutcome.Rejected,
            CustomerBillingExternalTransportOutcome.RetryableUnavailable => CustomerBillingDeliveryDispatcherOutcome.RetryableFailure,
            CustomerBillingExternalTransportOutcome.Indeterminate => CustomerBillingDeliveryDispatcherOutcome.Indeterminate,
            CustomerBillingExternalTransportOutcome.ConfigurationError or CustomerBillingExternalTransportOutcome.Disabled =>
                CustomerBillingDeliveryDispatcherOutcome.ConfigurationError,
            CustomerBillingExternalTransportOutcome.Simulated => CustomerBillingDeliveryDispatcherOutcome.ConfigurationError,
            _ => CustomerBillingDeliveryDispatcherOutcome.Indeterminate
        };
        var failureCategory = transportResult.Outcome switch
        {
            CustomerBillingExternalTransportOutcome.Rejected => CustomerBillingDeliveryFailureCategory.ProviderRejected,
            CustomerBillingExternalTransportOutcome.RetryableUnavailable => CustomerBillingDeliveryFailureCategory.RetryableProviderUnavailable,
            CustomerBillingExternalTransportOutcome.ConfigurationError or CustomerBillingExternalTransportOutcome.Disabled or CustomerBillingExternalTransportOutcome.Simulated =>
                CustomerBillingDeliveryFailureCategory.ConfigurationMissing,
            _ => (CustomerBillingDeliveryFailureCategory?)null
        };
        var failureCode = transportResult.Outcome == CustomerBillingExternalTransportOutcome.Simulated
            ? "development_transport_no_external_call"
            : providerCode;

        var persistence = await TryPersistTransportOutcomeAsync(
            deliveryId,
            transportResult.Outcome,
            failureCategory,
            failureCode,
            externalObjectId,
            externalRequestId);
        if (!persistence.OutcomePersisted)
        {
            return CreateResult(
                claimedDelivery,
                CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                "outcome_persistence_failed",
                attemptCount,
                externalObjectId,
                externalRequestId,
                providerCode,
                persistence.IndeterminateRecorded
                    ? CustomerBillingDeliveryState.Indeterminate
                    : CustomerBillingDeliveryState.Dispatching);
        }

        var state = outcome switch
        {
            CustomerBillingDeliveryDispatcherOutcome.Delivered => CustomerBillingDeliveryState.Delivered,
            CustomerBillingDeliveryDispatcherOutcome.Rejected or CustomerBillingDeliveryDispatcherOutcome.RetryableFailure
                or CustomerBillingDeliveryDispatcherOutcome.ConfigurationError => CustomerBillingDeliveryState.Failed,
            _ => CustomerBillingDeliveryState.Indeterminate
        };
        return CreateResult(
            claimedDelivery,
            outcome,
            providerCode,
            attemptCount,
            externalObjectId,
            externalRequestId,
            providerCode,
            state);
    }

    public async Task<CustomerBillingDeliveryDispatcherResult> ReconcileAsync(
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var delivery = await deliveryStore.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null)
        {
            return Missing(deliveryId);
        }

        if (delivery.DeliveryState != CustomerBillingDeliveryState.Indeterminate)
        {
            return CreateResult(delivery, CustomerBillingDeliveryDispatcherOutcome.NotEligible, "delivery_not_indeterminate");
        }

        if (!await deliveryStore.RecordReconciliationAttemptAsync(deliveryId, timeProvider.GetUtcNow(), cancellationToken))
        {
            var current = await deliveryStore.GetByIdAsync(deliveryId, cancellationToken);
            return current is null
                ? Missing(deliveryId)
                : CreateResult(
                    current,
                    CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                    "reconciliation_attempt_persistence_failed");
        }

        CustomerBillingExternalReconciliationResult reconciliationResult;
        try
        {
            reconciliationResult = await transport.ReconcileAsync(
                new CustomerBillingExternalReconciliationRequest(
                    delivery.ExternalIdempotencyKey,
                    delivery.ExternalObjectId,
                    delivery.Id),
                cancellationToken);
        }
        catch (Exception)
        {
            logger.LogWarning("Customer billing reconciliation failed without a definitive result. DeliveryId: {DeliveryId}", deliveryId);
            return CreateResult(delivery, CustomerBillingDeliveryDispatcherOutcome.StillIndeterminate, "reconciliation_exception");
        }

        if (reconciliationResult is null || !Enum.IsDefined(reconciliationResult.Outcome))
        {
            logger.LogWarning("Customer billing reconciliation returned an invalid result. DeliveryId: {DeliveryId}", deliveryId);
            return CreateResult(delivery, CustomerBillingDeliveryDispatcherOutcome.StillIndeterminate, "invalid_reconciliation_result");
        }

        var reasonCode = BoundedCode(reconciliationResult.ReasonCode, "reconciliation_outcome");
        var externalObjectId = BoundedIdentifier(reconciliationResult.ExternalObjectId);
        var externalRequestId = BoundedIdentifier(reconciliationResult.ExternalRequestId);
        switch (reconciliationResult.Outcome)
        {
            case CustomerBillingExternalReconciliationOutcome.FoundDelivered:
                if (!await TryRecordReconciledDeliveredAsync(deliveryId, externalObjectId, externalRequestId))
                {
                    return CreateResult(
                        delivery,
                        CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                        "reconciliation_persistence_failed",
                        externalObjectId: externalObjectId,
                        externalRequestId: externalRequestId,
                        providerCode: reasonCode);
                }

                return CreateResult(
                    delivery,
                    CustomerBillingDeliveryDispatcherOutcome.Delivered,
                    reasonCode,
                    externalObjectId: externalObjectId,
                    externalRequestId: externalRequestId,
                    providerCode: reasonCode,
                    state: CustomerBillingDeliveryState.Delivered);

            case CustomerBillingExternalReconciliationOutcome.FoundRejected:
                if (!await TryRecordReconciledRejectedAsync(deliveryId, reasonCode, externalRequestId))
                {
                    return CreateResult(
                        delivery,
                        CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure,
                        "reconciliation_persistence_failed",
                        externalRequestId: externalRequestId,
                        providerCode: reasonCode);
                }

                return CreateResult(
                    delivery,
                    CustomerBillingDeliveryDispatcherOutcome.Rejected,
                    reasonCode,
                    externalRequestId: externalRequestId,
                    providerCode: reasonCode,
                    state: CustomerBillingDeliveryState.Failed);

            case CustomerBillingExternalReconciliationOutcome.NotFound:
                return CreateResult(delivery, CustomerBillingDeliveryDispatcherOutcome.NotFound, reasonCode);
            case CustomerBillingExternalReconciliationOutcome.Unsupported:
                return CreateResult(delivery, CustomerBillingDeliveryDispatcherOutcome.Unsupported, reasonCode);
            default:
                return CreateResult(delivery, CustomerBillingDeliveryDispatcherOutcome.StillIndeterminate, reasonCode);
        }
    }

    private (CustomerBillingExternalMapping? Mapping, string? ErrorCode) ResolveMapping(
        CustomerBillingDelivery delivery)
    {
        var options = usageBillingOptions.Value;
        if (!options.Enabled)
        {
            return (null, "usage_billing_disabled");
        }

        if (!new CustomerUsageBillingOptionsValidator().Validate(null, options).Succeeded)
        {
            return (null, "usage_price_mapping_invalid");
        }

        var metric = delivery.Metric;
        var metricName = metric.ToString();
        var exactMetric = options.Metrics?
            .FirstOrDefault(entry => string.Equals(entry.Key, metricName, StringComparison.Ordinal));
        if (exactMetric is null
            || exactMetric.Value.Value is null
            || !CustomerUsageBillingOptionsValidator.IsValidMetricMapping(metric, exactMetric.Value.Value))
        {
            return (null, "usage_metric_mapping_invalid");
        }

        var metricMapping = exactMetric.Value.Value;
        var mappingKey = CustomerUsageBillingOptionsValidator.PriceMappingKey(
            delivery.SubscriptionPlanId,
            delivery.CustomerUsagePriceVersionId);
        if (!metricMapping.PriceMappings.TryGetValue(mappingKey, out var configured)
            || configured is null)
        {
            return (null, "usage_price_mapping_missing");
        }

        return (new CustomerBillingExternalMapping(
            metricMapping.Transport,
            configured.MeterEventName,
            metricMapping.CustomerPayloadKey,
            metricMapping.ValuePayloadKey,
            metricMapping.BillableQuantityIsExternalUnit)
        {
            PlanSnapshotId = delivery.SubscriptionPlanId,
            CustomerUsagePriceVersionId = delivery.CustomerUsagePriceVersionId,
            MeterId = configured.MeterId,
            UsagePriceId = configured.UsagePriceId,
            Currency = configured.Currency,
            UnitQuantity = configured.UnitQuantity,
            UnitPrice = configured.UnitPrice,
            StripeUnitAmountMinor = configured.StripeUnitAmountMinor,
            RateSemantics = configured.RateSemantics,
            MappingIdentity = configured.MappingIdentity
        }, null);
    }

    private static bool HasValidBillingSnapshot(CustomerBillingDelivery delivery) =>
        delivery.BillingStateSnapshot == CustomerBillableUsageState.Calculated
        && delivery.BillingReasonSnapshot == CustomerBillableUsageReason.Calculated
        && delivery.CompanyId != Guid.Empty
        && delivery.TenantSubscriptionId != Guid.Empty
        && delivery.SubscriptionPlanId != Guid.Empty
        && delivery.CustomerUsagePriceVersionId != Guid.Empty
        && delivery.DeliveryProvider == CustomerBillingDeliveryProvider.Stripe
        && delivery.DeliveryKind == CustomerBillingDeliveryKind.PendingExternalBilling
        && delivery.BillableQuantitySnapshot > 0
        && delivery.ChargeAmountSnapshot > 0
        && delivery.CurrencySnapshot == "USD"
        && delivery.PeriodStartUtc.Offset == TimeSpan.Zero
        && delivery.PeriodEndUtc.Offset == TimeSpan.Zero
        && delivery.PeriodStartUtc < delivery.PeriodEndUtc
        && delivery.OccurredAtUtc.Offset == TimeSpan.Zero
        && delivery.OccurredAtUtc >= delivery.PeriodStartUtc
        && delivery.OccurredAtUtc <= delivery.PeriodEndUtc
        && !string.IsNullOrWhiteSpace(delivery.ExternalIdempotencyKey)
        && delivery.ExternalIdempotencyKey.Length <= 100;

    private async Task<(bool OutcomePersisted, bool IndeterminateRecorded)> TryPersistTransportOutcomeAsync(
        Guid deliveryId,
        CustomerBillingExternalTransportOutcome transportOutcome,
        CustomerBillingDeliveryFailureCategory? failureCategory,
        string failureCode,
        string? externalObjectId,
        string? externalRequestId)
    {
        try
        {
            var occurredAtUtc = timeProvider.GetUtcNow();
            var persisted = transportOutcome switch
            {
                CustomerBillingExternalTransportOutcome.Delivered =>
                    await deliveryStore.RecordDeliveredAsync(deliveryId, externalObjectId, externalRequestId, occurredAtUtc),
                CustomerBillingExternalTransportOutcome.Rejected or CustomerBillingExternalTransportOutcome.RetryableUnavailable
                    or CustomerBillingExternalTransportOutcome.ConfigurationError or CustomerBillingExternalTransportOutcome.Disabled
                    or CustomerBillingExternalTransportOutcome.Simulated =>
                    await deliveryStore.RecordFailedAsync(
                        deliveryId,
                        failureCategory!.Value,
                        failureCode,
                        externalRequestId,
                        occurredAtUtc),
                _ => await deliveryStore.RecordIndeterminateAsync(
                    deliveryId,
                    externalObjectId,
                    externalRequestId,
                    failureCode,
                    occurredAtUtc)
            };
            if (persisted)
            {
                return (true, false);
            }
        }
        catch (Exception)
        {
            logger.LogError("Customer billing transport outcome persistence failed. DeliveryId: {DeliveryId}", deliveryId);
        }

        var indeterminateRecorded = await TryRecordIndeterminateAsync(
            deliveryId,
            externalObjectId,
            externalRequestId,
            "outcome_persistence_failed");
        return (false, indeterminateRecorded);
    }

    private async Task<bool> TryRecordIndeterminateAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId,
        string failureCode)
    {
        try
        {
            return await deliveryStore.RecordIndeterminateAsync(
                deliveryId,
                externalObjectId,
                externalRequestId,
                failureCode,
                timeProvider.GetUtcNow());
        }
        catch (Exception)
        {
            logger.LogError("Could not persist an indeterminate customer billing delivery outcome. DeliveryId: {DeliveryId}", deliveryId);
            return false;
        }
    }

    private async Task<bool> TryRecordReconciledDeliveredAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId)
    {
        try
        {
            return await deliveryStore.RecordReconciledDeliveredAsync(
                deliveryId,
                externalObjectId,
                externalRequestId,
                timeProvider.GetUtcNow());
        }
        catch (Exception)
        {
            logger.LogError("Could not persist a reconciled delivered customer billing outcome. DeliveryId: {DeliveryId}", deliveryId);
            return false;
        }
    }

    private async Task<bool> TryRecordReconciledRejectedAsync(
        Guid deliveryId,
        string failureCode,
        string? externalRequestId)
    {
        try
        {
            return await deliveryStore.RecordReconciledRejectedAsync(
                deliveryId,
                failureCode,
                externalRequestId,
                timeProvider.GetUtcNow());
        }
        catch (Exception)
        {
            logger.LogError("Could not persist a reconciled rejected customer billing outcome. DeliveryId: {DeliveryId}", deliveryId);
            return false;
        }
    }

    private static CustomerBillingDeliveryDispatcherResult Missing(Guid deliveryId) =>
        new(
            deliveryId,
            CustomerBillingDeliveryDispatcherOutcome.NotFound,
            null,
            "delivery_not_found",
            null,
            null,
            null,
            0);

    private static CustomerBillingDeliveryDispatcherResult CreateResult(
        CustomerBillingDelivery delivery,
        CustomerBillingDeliveryDispatcherOutcome outcome,
        string reasonCode,
        int? attemptCount = null,
        string? externalObjectId = null,
        string? externalRequestId = null,
        string? providerCode = null,
        CustomerBillingDeliveryState? state = null) =>
        new(
            delivery.Id,
            outcome,
            state ?? delivery.DeliveryState,
            BoundedCode(reasonCode, "dispatcher_outcome"),
            BoundedIdentifier(externalObjectId) ?? delivery.ExternalObjectId,
            BoundedIdentifier(externalRequestId) ?? delivery.ExternalRequestId,
            providerCode is null ? null : BoundedCode(providerCode, "provider_outcome"),
            attemptCount ?? delivery.AttemptCount);

    private static string BoundedCode(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.' or ':')
            ? value
            : fallback;

    private static string? BoundedIdentifier(string? value) =>
        string.IsNullOrEmpty(value) || value.Length > 200 ? null : value;
}
