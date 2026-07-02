# Payments

## Business Purpose

Payments represent money movement for subscriptions, invoices, marketplace services, and customer balances. This domain should support reliable payment tracking while avoiding storage of sensitive payment credentials.

## User Stories

- As a customer, I want to pay invoices using supported local and card payment methods.
- As finance, I want payments reconciled with invoices and refunds.
- As support, I want to see whether a payment is pending, successful, failed, or reversed.

## Functional Requirements

- Track payment amount, currency, provider, provider reference, status, method type, invoice, company, timestamp, and failure reason.
- Support pending, processing, succeeded, failed, cancelled, reversed, and refunded states.
- Link payments to invoices, subscriptions, marketplace requests, and refunds.
- Support future providers such as card processors, M-Pesa, bank transfer, and mobile money.

## Non-Functional Requirements

- Sensitive payment credentials must never be stored in StayFlow AI.
- Payment events must be idempotent.
- Payment records must be auditable and reconciliable.
- Payment workflows must tolerate delayed provider callbacks.

## Validation Rules

- Payment amount must be positive.
- Currency is required.
- Provider reference should be unique per payment provider.
- Successful payment must reference a valid invoice or billable transaction where applicable.
- Failed payment should capture a safe failure reason.

## Edge Cases

- Provider sends duplicate webhook events.
- Payment succeeds after invoice is marked overdue.
- Customer pays the wrong amount.
- Payment provider reverses a transaction.
- Manual bank transfer requires reconciliation.

## Acceptance Criteria

- Payment documentation defines status, provider references, reconciliation, and security boundaries.
- Requirements support future local and international payment methods.
- Edge cases cover duplicate callbacks, delayed success, wrong amounts, reversals, and manual payments.

## Future Enhancements

- M-Pesa integration.
- Card payment integration.
- Automated reconciliation dashboard.
- Payment retry workflows.
