# Refunds

## Business Purpose

Refunds document how StayFlow AI handles money returned to customers, hosts, or guests due to cancellation, service failure, overpayment, duplicate payment, or billing adjustment.

## User Stories

- As a customer, I want refunds to be clear, trackable, and timely.
- As support, I want refund status visible when handling disputes.
- As finance, I want refunds reconciled against invoices, payments, and commissions.

## Functional Requirements

- Track refund amount, currency, reason, status, payment reference, invoice, marketplace request, requester, approver, provider reference, and timestamp.
- Support requested, approved, processing, succeeded, failed, cancelled, and rejected states.
- Link refunds to original payments and affected invoices or marketplace transactions.
- Capture approval requirements for refunds, credits, and reversals.

## Non-Functional Requirements

- Refund records must be auditable.
- Refund processing must be idempotent.
- Refund decisions must be transparent to support and finance users.
- Refund data must be company isolated.

## Validation Rules

- Refund amount must be positive and must not exceed refundable balance unless explicitly approved.
- Currency must match the original payment unless provider rules allow otherwise.
- Refund reason is required.
- Approved refunds must include approver metadata.
- Failed refunds must capture a safe failure reason.

## Edge Cases

- Original payment method is unavailable.
- Partial refund is requested.
- Refund is approved but provider fails processing.
- Marketplace commission must be reversed.
- Customer asks for refund after cancellation policy window.

## Acceptance Criteria

- Refund documentation defines status, approval, reconciliation, and provider-processing expectations.
- Requirements separate refund decisions from payment provider execution.
- Edge cases cover partial refunds, provider failure, commission reversal, and policy disputes.

## Future Enhancements

- Customer-visible refund tracking.
- Refund policy automation.
- Credit balance support.
- Refund analytics by reason and service category.
