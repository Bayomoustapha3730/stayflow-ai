# Acceptance Criteria

## Executive Summary

This document provides testable Given / When / Then acceptance criteria for the Reservation domain.

## Business Purpose

Acceptance criteria align product, engineering, QA, security, and AI reviewers before implementation.

## Scope

In scope: manual creation, direct booking, external import, duplicate detection, lifecycle transitions, check-in, active stay, checkout, extensions, cancellation, no-show, returning guests, multi-reservation ambiguity, tenant isolation, AI context, and sensitive access protection.

Out of scope: endpoint schemas and database migrations.

## Actors

- Host.
- Property manager.
- Company administrator.
- Primary guest.
- AI concierge.
- QA reviewer.

## User Stories

- As a product owner, I want Reservation behavior captured in testable scenarios.
- As a host, I want stay workflows protected from incorrect automation.
- As a guest, I want accurate support for my current reservation.

## Functional Requirements

- Reservation behavior must support the scenarios below and align with [Reservation Lifecycle](ReservationLifecycle.md), [Reservation AI Context](ReservationAIContext.md), and [Guest Lifecycle](../guest/GuestLifecycle.md).

## Non-Functional Requirements

- Scenarios must be deterministic and suitable for future automated acceptance tests.
- Security and privacy expectations must be testable.

## Business Rules

- Reservation, property, and guest ownership must match Company ID.
- Status changes follow documented transition rules.
- AI never confirms approval-sensitive actions without approved workflow data.

## Validation Rules

- Required fields must be present for confirmation.
- Check-out must be after check-in.
- Source-aware duplicate rules must be applied.

## Error Handling

- Invalid input returns validation errors.
- Invalid transitions are rejected.
- Cross-tenant attempts are rejected.
- Ambiguous active reservations trigger clarification or escalation.

## Security Considerations

Acceptance tests must include access instruction protection and cross-tenant denial.

## Privacy Considerations

Scenarios must verify minimization of additional guest data and AI context exclusions.

## Multi-Tenant Considerations

All scenarios are company-scoped. No Company A user may access Company B reservations.

## AI Considerations

AI context scenarios must verify allowed and excluded data, and must handle multiple active reservations safely.

## Edge Cases

- Imported reservation lacks financial data.
- External reference is duplicated across sources.
- Guest phone number maps to multiple active reservations.
- Late checkout conflicts with next stay.

## Future Enhancements

- Convert scenarios to automated tests.
- Add API-specific acceptance tests.
- Add PMS import contract tests.

## Acceptance Criteria

### Manual Reservation Creation

```gherkin
Given a company user has a company-scoped property and primary guest
When they create a manual reservation with valid check-in and check-out dates
Then the reservation is created under that company
And the property and primary guest associations are tenant-valid
```

### Direct Booking Reservation

```gherkin
Given a company accepts a direct booking
When the reservation is recorded with source Direct Booking
Then no external platform reference is required
And the reservation can move through the standard lifecycle
```

### External Reservation Import

```gherkin
Given a controlled import includes source Airbnb and an external reference
When the reservation is imported
Then the external reference is stored with the source
And the import does not require direct Airbnb API access
```

### Duplicate Reservation Detection

```gherkin
Given a company already has a reservation with the same source and external reference
When another reservation with the same identifiers is imported
Then the system identifies a deterministic duplicate
And does not create a silent duplicate
```

### Confirmed Reservation

```gherkin
Given a reservation has valid company, property, guest, dates, source, and guest count
When it is confirmed
Then the status changes to Confirmed
And audit metadata records the transition
```

### Pre-Arrival Transition

```gherkin
Given a confirmed upcoming reservation
When pre-arrival workflows begin
Then the reservation moves to Pre-Arrival
And guest identification, language, consent, and readiness checks are evaluated
```

### Check-In

```gherkin
Given a reservation is Ready for Check-In and the guest is verified
When check-in is confirmed
Then the reservation moves to Checked In
And access instruction delivery is recorded without exposing secrets in logs
```

### Failed Check-In

```gherkin
Given a guest cannot access the property during check-in
When the issue is reported
Then the reservation remains in the appropriate check-in state
And an escalation is created for host or emergency support
```

### Active Stay

```gherkin
Given a checked-in reservation is within stay dates
When the stay begins
Then the reservation moves to Active Stay
And AI, WhatsApp, service request, and property knowledge workflows can use the selected reservation context
```

### Checkout

```gherkin
Given a reservation is in Active Stay
When the checkout window begins
Then the reservation moves to Check-Out Pending
And checkout instructions may be sent if communication eligibility allows
```

### Late Checkout Request

```gherkin
Given a guest requests late checkout
When no host or property rule approval exists
Then AI must not approve the request
And the request is routed for approval or denial
```

### Reservation Extension

```gherkin
Given a guest requests a new checkout date
When availability, host approval, source constraints, and payment considerations are confirmed
Then the reservation dates may be updated
And the guest can receive confirmed extension communication
```

### Cancellation

```gherkin
Given a reservation is cancelled by an external platform
When the cancellation event is received
Then the reservation moves to Cancelled
And StayFlow AI does not independently determine the external platform refund policy
```

### No Show

```gherkin
Given a guest does not arrive for an eligible confirmed reservation
When no-show is recorded by an authorized user or source
Then the reservation moves to No Show
And check-in automation stops
```

### Returning Guest Reservation

```gherkin
Given a primary guest has prior completed reservations within the same company
When a new reservation is created for that guest
Then the guest may be identified as returning
And the status does not expose stays from other companies
```

### Multiple Reservations For One Guest

```gherkin
Given one guest has multiple reservations
When a stay-specific workflow runs
Then the workflow selects the reservation by date, status, property, and company
And asks for clarification if selection is ambiguous
```

### Multiple Active Reservations Associated With One Phone Number

```gherkin
Given one phone number maps to multiple active reservations
When the guest asks a property-specific question
Then AI does not assume which property applies
And asks for clarification or escalates
```

### Cross-Tenant Reservation Access

```gherkin
Given Company A and Company B have reservations
When a Company A user requests a Company B reservation
Then access is rejected
And Company B reservation details are not disclosed
```

### Cross-Tenant Property Association

```gherkin
Given a Company A reservation request references a Company B property
When the reservation is created or updated
Then the association is rejected
And no reservation is created with mismatched tenant ownership
```

### Cross-Tenant Guest Association

```gherkin
Given a Company A reservation request references a Company B guest
When the reservation is created or updated
Then the association is rejected
And no cross-company guest data is disclosed
```

### AI Context Generation

```gherkin
Given a guest has a valid active reservation
When Reservation AI Context is generated
Then it includes only relevant reservation status, property, dates, stay phase, approved requests, preferred language, approved preferences, and relevant service requests
And excludes internal notes, unrelated financial information, audit logs, other guest information, and sensitive identifiers
```

### Sensitive Access Instruction Protection

```gherkin
Given a guest asks for door or lock instructions
When the reservation is cancelled, expired, unrelated, or the guest is unverified
Then the instructions are not sent
And the event is clarified or escalated according to policy
```
