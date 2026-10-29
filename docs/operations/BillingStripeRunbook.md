# Billing Stripe Runbook

## Purpose

Operational response guide for subscription failures, webhook incidents, and customer billing lifecycle issues.

## Key Signals

- `POST /api/billing/webhook/stripe` non-2xx responses
- Increase in subscription `PastDue` state
- Missing invoice synchronization
- Repeated `stripe_http_error` from billing provider

## Standard Checks

1. Verify Stripe endpoint health in dashboard webhook logs.
2. Inspect backend logs for webhook signature failures and payload size rejections.
3. Confirm `Billing` configuration values exist in active environment.
4. Validate plan-to-price mapping for requested plan changes.
5. Confirm API requests use the configured `Billing:StripeApiVersion`.

## Incident Procedures

### 1. Webhook Signature Failures

- Symptom: Unauthorized/forbidden webhook processing.
- Actions:
  - Confirm `StripeWebhookSigningSecret` matches current Stripe endpoint secret.
  - Validate server clock skew is within `WebhookToleranceSeconds`.
  - Replay a known-good event from Stripe dashboard after correction.

### 2. Duplicate Event Storm

- Symptom: frequent duplicate deliveries, potential concern over double application.
- Actions:
  - Verify duplicate responses are marked via billing webhook idempotency records.
  - Confirm no duplicate invoices/subscription regressions.
  - Keep endpoint returning `200` for duplicate events.

### 3. Plan Change Failures

- Symptom: upgrade/downgrade returns error.
- Actions:
  - Check that local plan exists and maps to valid Stripe `price_*` ID.
  - Confirm subscription has valid external subscription ID.
  - Review Stripe API status and retry outcome.

### 4. Cancel/Resume Mismatch

- Symptom: local state differs from Stripe dashboard.
- Actions:
  - Trigger `GET /api/billing/subscription` after action completion.
  - Confirm webhook events were received and applied.
  - If needed, force reconciliation by invoking action again from Owner/Admin account.

## Usage Billing Readiness (Disabled by Default)

Usage billing must remain disabled until product, finance, and operations approve
the usage policy and verify all externally provisioned resources. Both
`Billing:UsageBilling:Enabled` and `Billing:UsageBilling:Worker:Enabled` must
remain `false` until that activation gate is complete.

The worker only emits meter events. It must never create Stripe meters,
products, prices, or subscription items. Provision those resources separately.
Configure exact usage mappings by plan snapshot, metric, and immutable customer
usage price-version ID. A missing or mismatched mapping is a configuration
failure; do not substitute a current plan or current price.

`Billing:StripeApiVersion` is pinned to `2024-06-20` for both HTTP providers.
This version supports meter events and the subscription/invoice schemas consumed
by the custom provider. Configure the Stripe webhook endpoint to that same
version; a signed event declaring another version is rejected. Do not upgrade
the pin without reviewing response and webhook schemas together.

### Mapping Contract

`Billing:UsageBilling:Metrics` remains empty in checked-in configuration.
Only `Reservations`, `AiRequests`, and `WhatsAppMessages` have approved
production consumption units. `AiTokens`, `ApiRequests`, `StorageBytes`, and
`FileUploads` must not be externally mapped. `Users` and `Properties` remain
capacity metrics.

Each metric declares `Transport=StripeMeter`, distinct `CustomerPayloadKey`
and `ValuePayloadKey`, and `BillableQuantityIsExternalUnit=true`.
`PriceMappings` keys are canonical lowercase `plan-guid/price-version-guid`
pairs (not colon-delimited keys, because configuration binding reserves colons).
Each mapping requires:

- `MeterId`, `MeterEventName`, and `UsagePriceId`, unique across immutable routes.
  Meter IDs accept `mtr_` followed by an alphanumeric suffix, or `mtr_test_`
  followed by an alphanumeric suffix. Identifier acceptance alone does not
  verify the resource or satisfy `ResourcesVerified`.
- `Currency=USD`, `UnitQuantity`, and `UnitPrice`, exactly matching the durable
  billable operation's pricing snapshot.
- `StripeUnitAmountMinor`, the exact per-billable-unit rate in USD cents,
  representable with at most 12 decimal places. Rounded approximations fail
  parity; this phase does not define invoice-rounding or tiered pricing policy.
- `RateSemantics=LinearPerBillableUnit`.
- `MappingIdentity=plan-guid:Metric:price-version-guid:meter-id:event-name:price-id`.
- `ResourcesVerified=true` only after operations verifies and records external
  evidence that this exact price is USD, recurring metered, per-unit, un-tiered,
  has no included allowance or quantity transform, uses this meter with SUM
  aggregation, and is attached to the intended subscription. Every historical
  route must remain provisioned and attached for its dispatch lifetime.

These are explicit operational attestations, not live Stripe discovery.
Configuration alone cannot discover Stripe resource drift; changes to external
resources invalidate the attestation and require pausing the worker.
The runtime never looks up current pricing or provisions commercial resources.

The outbox is staged with the billable operation in the existing quota
transaction and committed with usage. Replay reuses the unique
operation/provider/kind delivery, including repairing a missing historical
outbox row. A coordinator failure rolls back admission rather than leaving a
partially consumed entitlement. Calculated zero charge is suppressed;
Unknown and NotBillable do not create external deliveries.

The emitted event quantity is the persisted
`CustomerBillingDelivery.BillableQuantitySnapshot`; included allowances remain
owned by StayFlow. Its event timestamp is the billable operation's persisted
`OccurredAtUtc`. Do not send raw or cumulative usage, provider cost, a
recalculated charge, or an invoice-item amount.

### Indeterminate Meter Event

Stripe does not provide a verified per-event lookup mechanism to reconcile a
meter event by its identifier. Therefore `Indeterminate` is not safe to resend
automatically, even if a dashboard search does not show the event.
Reconciliation strength remains **Weak**; individual event reconciliation is
`Unsupported`, never an invented exact match against aggregate usage.

Missing customer/configuration/mapping and currency/parity mismatches are
`ConfigurationError`, as are HTTP 401/403/404 and a `resource_missing` error.
Recognized deterministic HTTP 400 parameter-validation errors are `Rejected`.
HTTP 429 is `RetryableUnavailable`. HTTP 409, duplicate/idempotency ambiguity,
unproven HTTP 400 errors, HTTP 5xx, network/timeout/cancellation after dispatch,
and success followed by local outcome persistence failure are `Indeterminate`.
The latter reports a persistence failure locally while preserving the
indeterminate delivery state (or leaving Dispatching for abandoned-claim
recovery if persistence remains unavailable). No indeterminate event is
automatically resent.

1. Pause the usage worker and retain the delivery in `Indeterminate`.
2. Record the delivery ID, event identifier, event timestamp, metric, quantity,
   Stripe request ID (if available), and relevant Stripe request-log evidence.
3. Use Stripe request logs or Stripe Support to determine whether the event was
   accepted. A lack of visible aggregate usage is not proof of non-acceptance.
4. Record the authoritative external outcome through the approved manual
   incident procedure. If acceptance cannot be proven either way, leave the
   delivery unresolved; never issue a replacement event speculatively.
5. Resume the worker only after the incident owner confirms that no unresolved
   delivery can be automatically resent.

There is no public manual-resolution endpoint in this phase. After independent
review of authoritative Stripe evidence, an approved tenant-scoped maintenance
operation can use the existing conditional store transitions
`RecordReconciledDeliveredAsync` (accepted) or `RecordReconciledRejectedAsync`
(definitively rejected), retaining external identifiers and incident evidence.
These only transition an `Indeterminate` delivery and do not send an event.
An unsupported lookup or missing aggregate usage is not sufficient evidence for
either transition. Never reset an ambiguous delivery to Prepared or retry it.

### Late Usage and Commercial Changes

No automatic late-event billing policy is defined. A delivery whose period has
closed, whose invoice may be finalized, or whose persisted occurrence is
outside Stripe's accepted timestamp window must fail closed for operational
review. Do not shift its event timestamp or silently move it to another period.

Mid-period price changes use the operation's immutable price-version snapshot;
the exact historical mapping must remain configured. Mid-period plan changes
use the operation's persisted plan snapshot, not the subscription's current
plan. Missing historical configuration is a configuration error, not grounds
to fall back to today's plan or price. Finance must decide how to handle
finalized invoices and any delayed delivery before usage billing is activated.
`OpenPeriodInvoiceSafetyVerified` remains `false`: even an open-period delivery
fails with `ConfigurationError` until operations can prove that its usage
invoice cannot finalize before period close. A base-subscription invoice or a
missing local invoice is not such proof. If that external lifecycle guarantee
cannot be made, leave this guard false and implement a separate invoice-safety
policy phase. No post-close send or timestamp shifting is permitted.

The existing hard-quota rule is unchanged: usage above a finite allowance is
rejected before billing calculation. Positive overage is therefore generally
unreachable in today's production admission path. Allowing overage is a separate
product and architecture decision, not part of enabling these transports.

### Safe Activation Order

1. Approve included-allowance, overage, invoice-finalization, and late-usage
   policies with product and finance.
2. Provision and verify Stripe meters and usage prices outside the application;
   verify their meter/event/currency/rate relationship.
3. Configure exact plan/metric/price-version mappings and pin the Stripe API
   version on both API requests and the Stripe webhook endpoint.
4. Run parity and failure-mode tests with both switches still disabled.
5. Approve a monitored rollout plan, then enable usage billing before enabling
   the worker. Keep the worker disabled until mappings and delivery backlog are
   reviewed.

## Recovery Validation

- Checkout and portal launches succeed.
- Subscription lifecycle endpoints succeed for Owner/Admin and deny unauthorized roles.
- Usage summary and invoice history return tenant-scoped data.

## Security and Access

- Billing actions must only be executed by Owner/Admin.
- Never paste Stripe secrets in tickets/chat.
- Share only non-sensitive Stripe identifiers (`cus_*`, `sub_*`, `in_*`) when escalating.
