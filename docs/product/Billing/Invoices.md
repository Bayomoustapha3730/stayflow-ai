# Invoices

## Business Purpose

Invoices provide formal billing records for subscriptions, add-ons, marketplace commissions, taxes, refunds, and adjustments. They help customers understand charges and give StayFlow AI a financial audit trail.

## User Stories

- As a company owner, I want invoices that clearly show what I was charged for.
- As finance, I want invoice records that reconcile with payments and refunds.
- As support, I want invoice details available when customers ask billing questions.

## Functional Requirements

- Store invoice number, company, billing period, line items, subtotal, discounts, taxes, total, amount paid, balance, status, issue date, due date, and payment references.
- Support draft, open, paid, void, overdue, partially paid, and refunded statuses.
- Generate invoice line items for subscriptions, add-ons, marketplace fees, taxes, and adjustments.
- Support downloadable invoice records in future implementation.

## Non-Functional Requirements

- Invoice numbers must be unique and auditable.
- Invoice records should be immutable after finalization except for status and payment metadata.
- Invoice data must be company isolated.
- Totals should be deterministic and reconcilable.

## Validation Rules

- Invoice must belong to one company.
- Finalized invoice must have at least one line item.
- Currency is required for all monetary amounts.
- Due date should not precede issue date.
- Paid invoices must reference payment evidence.

## Edge Cases

- Invoice is generated but payment fails.
- Customer disputes a line item.
- Partial payment is received.
- Refund applies to a paid invoice.
- Tax rate changes during the billing period.

## Acceptance Criteria

- Invoice documentation defines status, line item, totals, and reconciliation expectations.
- Requirements support financial auditability.
- Edge cases cover failed payments, disputes, partial payment, refunds, and tax changes.

## Future Enhancements

- PDF invoice generation.
- Accounting export.
- Customer billing portal.
- Automated invoice email delivery.
