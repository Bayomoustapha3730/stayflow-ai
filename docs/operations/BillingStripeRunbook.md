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

## Recovery Validation

- Checkout and portal launches succeed.
- Subscription lifecycle endpoints succeed for Owner/Admin and deny unauthorized roles.
- Usage summary and invoice history return tenant-scoped data.

## Security and Access

- Billing actions must only be executed by Owner/Admin.
- Never paste Stripe secrets in tickets/chat.
- Share only non-sensitive Stripe identifiers (`cus_*`, `sub_*`, `in_*`) when escalating.
