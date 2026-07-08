# Cancellation

## Executive Summary

Cancellation documents how reservations are cancelled by guests, hosts, property managers, external platforms, or duplicate events.

## Business Purpose

Clear cancellation behavior prevents active stay automation from continuing after a reservation is no longer valid and protects StayFlow AI from incorrectly deciding third-party refund policies.

## Scope

In scope: guest cancellation, host cancellation, property manager cancellation, external platform cancellation, duplicate cancellation event, reason, timestamp, and communication behavior.

Out of scope: independent determination of Airbnb, Booking.com, Expedia, or other external refund policies.

## Actors

- Guest.
- Host.
- Property manager.
- External booking platform.
- Support agent.
- AI concierge.

## User Stories

- As a guest, I want cancellation status communicated clearly.
- As a host, I want cancelled reservations removed from active automation.
- As support, I want refund policy boundaries respected.

## Functional Requirements

- Store cancellation reason, timestamp, source, actor, status, and communication notes.
- Support idempotent duplicate cancellation events.
- Stop normal pre-arrival, check-in, and active stay automation.
- Link cancellation to payment or refund workflows without deciding external policy.

## Non-Functional Requirements

- Cancellation events must be auditable.
- External updates must be idempotent.
- Communication after cancellation must be policy-aware.

## Business Rules

- External platform refund decisions remain authoritative when applicable.
- AI must not promise refunds or override platform policy.
- Duplicate cancellation events should not create duplicate records.
- Cancelled is a terminal state unless reopened through privileged correction.

## Validation Rules

- Cancellation reason is required for manual cancellation.
- Source and timestamp are required.
- Cancelled reservation must not transition to Active Stay without reopening.

## Error Handling

- Duplicate cancellation event is ignored or logged idempotently.
- Refund ambiguity should escalate to human support.
- Cancellation after check-in requires manual review.

## Security Considerations

Cancellation can affect access and financial workflows; authorization and audit metadata are required.

## Privacy Considerations

Cancellation communication should avoid unnecessary disclosure of guest personal or financial data.

## Multi-Tenant Considerations

Cancellation events must validate company ownership before updating a reservation.

## AI Considerations

AI may explain cancellation status from reservation context but must not independently determine or promise third-party refunds.

## Edge Cases

- Guest cancels after check-in.
- External platform sends stale cancellation.
- Host cancellation creates relocation need.
- Duplicate cancellation arrives from import retry.

## Future Enhancements

- Cancellation policy reference model.
- Refund workflow integration.
- Availability reopening automation.

## Acceptance Criteria

- Cancellation source, reason, and timestamp are documented.
- External refund policy boundary is explicit.
- Duplicate cancellation handling is idempotent.
