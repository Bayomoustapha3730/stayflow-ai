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
