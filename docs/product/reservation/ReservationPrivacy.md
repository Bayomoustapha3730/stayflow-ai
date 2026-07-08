# Reservation Privacy

## Executive Summary

Reservation Privacy defines how stay records, guest relationships, access-sensitive information, financial references, notes, and AI context should be protected.

## Business Purpose

Reservations combine guest identity, property occupancy, dates, possible payment details, and operational notes. Mishandling this data can expose guests, hosts, and properties to privacy and security risk.

## Scope

In scope: data minimization, retention considerations, sensitive access information, internal notes, additional guest data, AI context exclusions, and tenant isolation.

Out of scope: legal advice and final retention schedule implementation.

## Actors

- Guest.
- Host.
- Property manager.
- Company administrator.
- Security reviewer.
- AI concierge.

## User Stories

- As a guest, I want stay details protected.
- As a host, I want access instructions shared only with eligible guests.
- As a security reviewer, I want AI prompts to exclude unnecessary reservation data.

## Functional Requirements

- Classify reservation fields by operational use, guest-facing use, internal staff use, and AI-eligible use.
- Exclude internal notes and unrelated financial data from AI by default.
- Protect sensitive access instructions.
- Support retention review for completed, cancelled, and no-show reservations.

## Non-Functional Requirements

- Privacy rules must be auditable.
- Data minimization must apply to AI, logs, and staff workflows.
- Tenant isolation must be enforced consistently.

## Business Rules

- Internal notes are staff-only.
- Other guest information must not be sent to AI or unrelated guests.
- Sensitive identifiers and audit logs are excluded from AI context.
- Retention actions must respect active disputes, payments, and operational obligations.

## Validation Rules

- Privacy-sensitive fields require scope and purpose before use.
- Access instructions require reservation eligibility.
- Cross-tenant data access must be rejected.

## Error Handling

- If privacy classification is unknown, use the most restrictive handling.
- If retention conflicts with active obligations, flag for review.
- If sensitive data appears in notes, trigger cleanup workflow in future implementation.

## Security Considerations

Access instructions, occupancy dates, internal notes, and payment-related fields require strong access control and audit logging.

## Privacy Considerations

Reservation privacy should align with [Guest Privacy](../guest/GuestPrivacy.md), especially consent, retention, and AI personalization boundaries.

## Multi-Tenant Considerations

Reservation privacy controls apply within Company ID. No cross-company identity or reservation sharing is allowed for MVP.

## AI Considerations

AI context must be minimized and purpose-specific. Internal notes, unrelated financial information, audit logs, other guest information, and sensitive identifiers are excluded by default.

## Edge Cases

- Guest asks about another guest on the reservation.
- Staff put access code in internal notes.
- Cancelled reservation still has payment dispute.
- Completed reservation reaches retention threshold.

## Future Enhancements

- Field-level privacy labels.
- Reservation retention automation.
- Sensitive content scanning.

## Acceptance Criteria

- Privacy field categories are documented.
- Sensitive access and AI exclusions are explicit.
- Retention considerations are covered.
