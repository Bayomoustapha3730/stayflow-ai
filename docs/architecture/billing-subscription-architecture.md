# Billing and Subscription Architecture

## Scope

Sprint 20B introduces tenant-safe Stripe billing orchestration for checkout, plan lifecycle actions, invoice synchronization, usage summaries, and self-service account management.

## Components

- API surface: `api/billing/*` endpoints in [backend/Controllers/BillingController.cs](backend/Controllers/BillingController.cs)
- Domain service: billing orchestration and webhook application in [backend/Services/BillingService.cs](backend/Services/BillingService.cs)
- Provider abstraction: Stripe and development providers in [backend/Services/Billing/BillingProviderContracts.cs](backend/Services/Billing/BillingProviderContracts.cs), [backend/Services/Billing/StripeBillingProvider.cs](backend/Services/Billing/StripeBillingProvider.cs), [backend/Services/Billing/DevelopmentBillingProvider.cs](backend/Services/Billing/DevelopmentBillingProvider.cs)
- Persistence: subscription, invoice, and webhook entities in [backend/Models/TenantSubscription.cs](backend/Models/TenantSubscription.cs), [backend/Models/TenantInvoice.cs](backend/Models/TenantInvoice.cs), [backend/Models/BillingWebhookEvent.cs](backend/Models/BillingWebhookEvent.cs)
- Frontend dashboard: billing workflows in [frontend/src/pages/BillingDashboardPage.tsx](frontend/src/pages/BillingDashboardPage.tsx)

## Request Flows

1. Checkout

- Owner/Admin calls `POST /api/billing/checkout` with selected plan and optional `trialDays`
- Service validates tenant membership and configured Stripe price mapping
- Service ensures a Stripe customer exists, then creates Stripe Checkout session
- Audit event `BillingCheckoutCreated` is recorded

2. Plan upgrade/downgrade

- Owner/Admin calls `POST /api/billing/subscription/change-plan`
- Service maps plan to Stripe price and updates subscription item with Stripe proration
- Snapshot is synchronized into local subscription state
- Audit event `BillingPlanChanged` is recorded

3. Cancellation and resume

- Owner/Admin calls `POST /api/billing/subscription/cancel` or `POST /api/billing/subscription/resume`
- Service forwards action to Stripe and re-synchronizes current period and status
- Audit events `BillingCancelScheduled`/`BillingCancelledImmediately`/`BillingSubscriptionResumed` are recorded

4. Payment method management

- Owner/Admin calls `POST /api/billing/portal/payment-method`
- Service creates Stripe Billing Portal session constrained to payment method flow

5. Webhook processing

- Stripe posts to `POST /api/billing/webhook/stripe`
- Signature is verified with webhook secret and timestamp tolerance
- Event is idempotently stored by `(Provider, EventId)` unique key
- Known event types update subscriptions/invoices and write audit logs
- Duplicate events are acknowledged without reapplying state

## Data and Synchronization Guarantees

- Source of truth for payment outcomes and subscription state is Stripe
- Local subscription and invoice records are a synchronized projection for tenant reads and authorization-scoped UX
- Event ordering protection uses `LastProviderEventCreatedAtUtc`
- Duplicate webhook deliveries are absorbed through pre-check plus unique-key collision handling

## Security Controls

- Admin-only billing action APIs; no client-side billing state trust
- Tenant context required for all billing reads/writes except webhook endpoint
- No storage of payment card details; only Stripe identifiers and status metadata
- Sensitive Stripe secrets are never returned to frontend

## Failure and Retry Strategy

- Stripe API calls use bounded retries for `408`, `409`, `429`, and `5xx`
- Webhook duplicate collisions are safely treated as idempotent success
- Snapshot sync failures on webhook are logged and do not break idempotent event acknowledgement

## Observability

- Audit log records for user-triggered and webhook-triggered billing changes
- Webhook processing result includes event identity and duplicate status for diagnostics

## Customer usage accounting visibility

- `CustomerBillableUsageOperations` are the historical monetary source of truth for consumed usage.
- Internal reports sum persisted `ChargeAmount` only for `Calculated` USD operations; `Unknown` is not zero, `NotBillable` is excluded, and a calculated zero remains calculated.
- Reports use the persisted `PeriodStartUtc`/`PeriodEndUtc` snapshots and select range periods by `PeriodStartUtc` in a half-open interval. Historical prices are never recalculated.
- Summary, breakdown, and accounting-reconciliation reads are restricted to the `platform.admin` authorization policy and permission.
- These reports reconcile usage-accounting facts only. No mapping to invoice line items exists yet, and no automatic Stripe usage submission or invoice reconciliation is performed.

## Customer billing delivery outbox

- **2D.5A — durable outbox:** `CustomerBillableUsageOperation` remains the authoritative internal accounting fact. A durable `CustomerBillingDelivery` snapshots that fact before any external transport can run.
- Each operation has at most one delivery per provider and kind. The persisted external idempotency key is derived from the durable delivery ID and remains stable across retries; retries never create a new delivery identity.
- Delivery is downstream of accounting and quota admission. It does not read AI provider cost telemetry, change quota behavior, or recalculate historical customer charges.
- Only `Calculated` USD facts with a persisted price version are eligible. A calculated zero-charge fact is retained as `Suppressed`; `Unknown` and `NotBillable` facts do not create external deliveries.
- Dispatch claims use conditional database updates. Only `Prepared` and explicitly retryable `Failed` rows can be claimed. `Delivered` and `Indeterminate` rows cannot be automatically redispatched; timeout/network failures are not automatically retryable unless positively classified as `RetryableProviderUnavailable`.
- An ambiguous external outcome remains `Indeterminate` until a later provider-reconciliation phase resolves it. Known outcomes are persisted without honoring caller cancellation, and a persistence failure cannot create a second local delivery or idempotency key.
- No invoice item, invoice, or payment collection is created by the customer usage delivery path.

## Stripe customer usage transport foundation

- **2D.5B — transport:** `Billing:PlanPriceIds` remains exclusively the base recurring subscription price mapping. `Billing:UsageBilling` is separate and defaults to `Enabled: false`; no production meter IDs or secrets are checked in.
- A usage metric must have an exact-name `StripeMeter` mapping, configured meter event/customer/value payload keys, and an explicit `BillableQuantityIsExternalUnit: true` assertion. Missing or unsupported mappings fail closed; there is no fallback to a plan price or inferred quantity conversion.
- Stripe meter events use the delivery's persisted idempotency key as both the Stripe event `identifier` and HTTP `Idempotency-Key`. The transport sends only the configured event name, mapped customer, and snapshotted billable quantity; the internal charge snapshot is not sent or repriced.
- The meter event timestamp is the snapshotted period start and is rejected if it is outside Stripe's accepted event-time window. No occurrence-time fact is inferred.
- Before a send, the Stripe adapter verifies the snapshotted subscription ID belongs to the same company and plan, has an external subscription mapping, is Active or Trialing, and that the company already has a Stripe customer mapping. It never creates customers.
- A Stripe 429 is returned as explicitly retryable unavailable, other 4xx responses are rejected, and network errors, timeouts, 5xx responses, or malformed success responses are indeterminate. The transport performs no POST retry. Meter-event reconciliation is explicitly unsupported by this adapter.
- Development transport outcomes are deterministic simulations and make no HTTP calls. The Stripe adapter remains behind the controlled dispatcher; the autonomous worker described below is disabled by default.

## Controlled customer billing dispatch and reconciliation

- **2D.5C1 — controlled dispatcher/reconciliation:** `CustomerBillingDeliveryDispatcher` is the only service that sends or reconciles external customer billing deliveries.
- `CustomerBillingDeliveryDispatcher` is an internal application service that can be invoked from application or test code. It is not exposed through tenant or platform-admin HTTP endpoints.
- A dispatch requires the durable store's atomic claim. One successful claim makes exactly one transport `SendAsync` call; claim losers do not call the transport. The persisted delivery snapshot and external idempotency key are passed unchanged, with no repricing or alternate delivery creation.
- Delivered outcomes are persisted as `Delivered`; definitive rejection is persisted as non-retryable `Failed`; only an explicit `RetryableUnavailable` result uses the existing `RetryableProviderUnavailable` category; ambiguous outcomes and unexpected transport exceptions are persisted as `Indeterminate`. Outcome writes do not honor caller cancellation. If a post-send write fails, the dispatcher makes a best-effort transition to `Indeterminate` and never sends again in that call.
- Reconciliation only accepts `Indeterminate` deliveries and calls the transport's `ReconcileAsync`; it never calls `SendAsync`. A confirmed delivery or rejection is conditionally persisted. `NotFound`, unsupported reconciliation, and unresolved results do not authorize a retry or alter the unresolved state.
- The dispatcher does not perform a historical sweep. No migration is required. `ProviderCostAmount` and AI provider telemetry are unrelated to customer charges, the delivery snapshot remains the financial source of truth, and hard-quota behavior is unchanged.

## Autonomous customer billing delivery worker

- **2D.5C2 — disabled-by-default worker:** `CustomerBillingDeliveryWorker` is a hosted background service that discovers bounded candidate IDs and performs all sends/reconciliation only through `ICustomerBillingDeliveryDispatcher`.
- Both `Billing:UsageBilling:Enabled` and `Billing:UsageBilling:Worker:Enabled` must be true before autonomous work can start. Both are false in checked-in configuration. Enabling the worker while usage billing is disabled fails startup validation.
- Discovery is advisory and does not reserve rows. The durable conditional dispatcher claim remains authoritative: at most one worker instance can claim a delivery and make its single send. Multiple instances may discover the same ID safely.
- Prepared deliveries are eligible immediately; `Failed` deliveries are discovered only for `RetryableProviderUnavailable` and only after the configured durable `LastAttemptAtUtc` retry delay. A retry can only occur in a later polling cycle; there is no inner retry loop.
- `Indeterminate` deliveries are excluded from dispatch and discovered separately for reconciliation after a bounded delay. `NotFound` and unsupported/unresolved reconciliation results remain unresolved and do not authorize a send.
- Old `Dispatching` deliveries are conditionally moved to `Indeterminate` using the existing attempt timestamp, without incrementing attempts or changing the idempotency key. They are reconciled later and are never resent automatically.
- Each polling cycle uses bounded dispatch, reconciliation, and stale-recovery batches; it does not sweep history without limit. Shutdown stops new item processing and preserves the dispatcher's outcome-safety behavior. Operational logs contain bounded counts and IDs only, not payment details, guest PII, or provider response bodies.
- There are no HTTP mutation controls or runtime UI toggles for the worker. Hard quota behavior is unchanged.
