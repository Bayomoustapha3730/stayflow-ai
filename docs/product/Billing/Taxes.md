# Taxes

## Business Purpose

Taxes define how StayFlow AI represents tax obligations for subscriptions, marketplace commissions, invoices, and financial reporting. The product must be ready for Kenyan tax requirements and future multi-country expansion.

## User Stories

- As finance, I want tax amounts shown clearly on invoices.
- As a company owner, I want billing documents that support accounting records.
- As a product owner, I want tax logic separated from base pricing so it can evolve.

## Functional Requirements

- Store tax type, tax rate, taxable amount, tax amount, jurisdiction, currency, invoice line item, and effective date.
- Support VAT-style taxes, withholding considerations, zero-rated items, exemptions, and future regional rules.
- Link tax records to invoice line items and billing events.
- Preserve historical tax rates for finalized invoices.

## Non-Functional Requirements

- Tax calculations must be deterministic and auditable.
- Tax rules must support effective dates.
- Tax data must be reportable for finance workflows.
- Tax handling must avoid hard-coding assumptions into unrelated billing features.

## Validation Rules

- Tax rate must be non-negative.
- Tax amount must include currency.
- Finalized invoice tax values should not change after issuance except through adjustment documents.
- Exemptions should include reason or classification.
- Jurisdiction should be captured when tax is applied.

## Edge Cases

- Tax rate changes during a billing period.
- Customer is tax exempt.
- Marketplace transaction has different tax treatment than subscription revenue.
- Invoice includes items with mixed tax rates.
- Tax is rounded differently by provider or accounting system.

## Acceptance Criteria

- Tax documentation defines tax records, effective dates, exemptions, and invoice linkage.
- Requirements support Kenyan operations and future multi-jurisdiction expansion.
- Edge cases cover rate changes, exemptions, mixed rates, and rounding differences.

## Future Enhancements

- Tax rule engine.
- Accounting system export.
- Tax report dashboard.
- Jurisdiction-aware invoice templates.
