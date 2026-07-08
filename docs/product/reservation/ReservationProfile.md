# Reservation Profile

## Executive Summary

The Reservation Profile defines the structured data required to represent a stay without requiring unavailable financial data or unnecessary additional guest information.

## Business Purpose

A consistent profile lets hosts, AI, WhatsApp, and operations workflows rely on the same reservation facts.

## Scope

In scope: identifiers, relationships, source references, dates, guest counts, status, optional booking amount, special requests, internal notes, and audit dates.

Out of scope: payment credentials, platform refund policy, and source-specific contract terms.

## Actors

- Property manager.
- Host.
- Company administrator.
- Import workflow.
- AI concierge.

## User Stories

- As a host, I want the reservation profile to show essential stay facts.
- As an import workflow, I want to store source references without assuming global uniqueness.
- As an AI workflow, I need safe fields for stay-specific responses.

## Functional Requirements

- Store Reservation ID, Company ID, Property ID, Primary Guest ID, external reservation reference, reservation source, confirmation number, check-in date/time, check-out date/time, adult count, child count, total guest count, status, booking currency, booking amount where available, special requests, internal notes, created date, and updated date.
- Distinguish guest-facing special requests from internal staff notes.
- Allow missing booking amount and currency when unavailable.

## Non-Functional Requirements

- Profile data must support search, reporting, and AI context filtering.
- Updates must be auditable.
- Internal notes must not be included in AI context by default.

## Business Rules

- Booking amount is optional.
- Booking currency is required only when booking amount is provided.
- Internal notes are staff-only.
- Special requests require review before AI treats them as approved.

## Validation Rules

- Required identifiers must be present before confirmation.
- Date range must be valid.
- Guest counts cannot be negative.
- Total guest count must align with adult and child counts when provided.

## Error Handling

- Missing optional financial data should produce no error.
- Invalid guest counts should return validation errors.
- Internal note exposure attempts should be blocked.

## Security Considerations

Internal notes may contain operationally sensitive information. Access should be limited to authorized company users.

## Privacy Considerations

Avoid storing unnecessary personal data in profile fields and notes.

## Multi-Tenant Considerations

Company ID must match associated Property and Primary Guest.

## AI Considerations

AI may use status, stay phase, dates, property, and approved special requests. AI must not receive internal notes by default.

## Edge Cases

- Imported reservation has no confirmation number.
- Source provides amount without reliable tax details.
- Staff records sensitive data in notes.

## Future Enhancements

- Field-level sensitivity labels.
- Reservation profile completion score.
- Source-specific profile validation.

## Acceptance Criteria

- Structured reservation data is documented.
- Optional financial data is not required.
- Internal notes are separated from AI-eligible context.
