# Commissions

## Business Purpose

Commissions define how StayFlow AI earns revenue from marketplace transactions such as drivers, cleaning, laundry, groceries, private chefs, tour guides, and maintenance services.

## User Stories

- As a marketplace provider, I want to understand the commission deducted from each completed service.
- As StayFlow AI finance, I want commissions calculated consistently and reported accurately.
- As a host, I want marketplace pricing to be transparent.

## Functional Requirements

- Track commission rate, fixed fee, transaction amount, provider payout, platform revenue, currency, service category, and settlement status.
- Support category-specific commission rules.
- Link commission records to marketplace service requests, invoices, payments, refunds, and provider payouts.
- Support manual adjustment with reason and audit metadata.

## Non-Functional Requirements

- Commission calculations must be deterministic and auditable.
- Historical commission rules must remain available for past transactions.
- Commission data must support finance reporting.
- Provider-facing values must be clear and explainable.

## Validation Rules

- Commission rate must be between 0 and 100 percent.
- Currency is required for monetary values.
- Completed marketplace transaction is required before final commission recognition.
- Manual commission adjustment requires actor and reason.
- Refunds must account for commission reversal or adjustment rules.

## Edge Cases

- Provider gives a discount after quote approval.
- Service is partially refunded.
- Commission rule changes mid-month.
- Provider payout fails.
- Host absorbs commission instead of provider.

## Acceptance Criteria

- Commission documentation defines calculation inputs, settlement, reporting, and adjustment expectations.
- Requirements support marketplace monetization without obscuring provider payouts.
- Edge cases cover discounts, partial refunds, rule changes, payout failure, and fee allocation.

## Future Enhancements

- Provider payout ledger.
- Commission rule engine.
- Monthly provider statements.
- Category-level margin analytics.
