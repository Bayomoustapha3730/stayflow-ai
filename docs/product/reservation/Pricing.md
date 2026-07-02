# Pricing

## Business Purpose

Reservation pricing defines how stay costs, fees, discounts, taxes, deposits, and adjustments are represented for guest communication and operational clarity. It should support accurate concierge responses without turning StayFlow AI into the source of financial truth before payment systems mature.

## User Stories

- As a guest, I want clear pricing information for my reservation and any extension.
- As a host, I want pricing notes connected to reservation changes and payments.
- As an operations user, I want discounts, fees, and adjustments visible for support.

## Functional Requirements

- Store nightly rate summary, total amount, currency, fees, taxes, discounts, deposits, payment status, and pricing notes.
- Link reservation pricing to payment records and extension quotes.
- Record pricing source, last updated timestamp, and manual adjustment reason.
- Support Kenyan Shilling and future multi-currency scenarios.

## Non-Functional Requirements

- Pricing information must be auditable.
- Guest-facing pricing messages must be clear and avoid unsupported guarantees.
- Price calculations should be deterministic when implemented.
- Sensitive payment details must not be stored in pricing notes.

## Validation Rules

- Currency must be present when monetary amounts are stored.
- Monetary values must not be negative unless explicitly modeled as discounts, credits, or refunds.
- Manual adjustments should require reason and actor.
- Payment status should not be inferred solely from pricing total.

## Edge Cases

- Booking platform total differs from local pricing record.
- Guest receives discount after confirmation.
- Extension price is quoted in a different currency.
- Refund or partial payment changes the balance.
- Taxes or service charges are unknown at import time.

## Acceptance Criteria

- Pricing documentation separates reservation pricing from payment processing.
- Requirements support fees, discounts, currency, adjustments, and extension quotes.
- Edge cases prepare the domain for booking platform and payment integration.

## Future Enhancements

- Dynamic pricing integration.
- Tax and fee rule engine.
- Automated balance calculation.
- Multi-currency display and exchange-rate handling.
