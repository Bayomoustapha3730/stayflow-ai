# Subscriptions

## Business Purpose

Subscriptions define how hosts and property managers pay for ongoing access to StayFlow AI. They support recurring SaaS revenue, account lifecycle management, plan limits, and billing-state driven access.

## User Stories

- As a company owner, I want to subscribe to a StayFlow AI plan that fits my portfolio size.
- As an administrator, I want subscription status to control access without disrupting active guest support unexpectedly.
- As finance, I want recurring subscription events to be auditable and reportable.

## Functional Requirements

- Track subscription status, billing period, renewal date, plan, company, payment method reference, cancellation state, and trial state.
- Support trial, active, past due, cancelled, expired, paused, and grace-period states.
- Link subscription changes to invoices and payments.
- Notify account owners before renewal, failed payment, cancellation, and access changes.

## Non-Functional Requirements

- Subscription checks must be reliable and low latency.
- Billing state must be auditable.
- Access restrictions should degrade gracefully for active guest operations.
- Subscription data must be company isolated.

## Validation Rules

- A subscription must belong to one company.
- Active subscriptions must reference a valid plan.
- Renewal date must be after the subscription start date.
- Cancellation reason should be captured when a user cancels manually.

## Edge Cases

- Payment fails during an active guest stay.
- Company upgrades mid-cycle.
- Company downgrades below current property or user usage.
- Trial expires without a payment method.
- Subscription is cancelled then reactivated.

## Acceptance Criteria

- Subscription documentation defines lifecycle states, billing period handling, and access impact.
- Requirements support future recurring billing integration.
- Edge cases cover failed payments, trials, upgrades, downgrades, and reactivation.

## Future Enhancements

- Self-service subscription management.
- Usage-based add-ons.
- Dunning workflows.
- Annual billing discounts.
