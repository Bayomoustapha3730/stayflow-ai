# Check-Out

## Executive Summary

Check-Out defines how a reservation moves from active stay toward departure, post-stay, and completion.

## Business Purpose

Checkout workflows help guests depart smoothly, protect property readiness, resolve open issues, and prepare post-stay communication.

## Scope

In scope: standard checkout, late checkout request, checkout instructions, departure confirmation, unresolved service requests, outstanding payment considerations, post-stay eligibility, and review request workflow.

Out of scope: automatic late checkout approval and payment settlement implementation.

## Actors

- Primary guest.
- Host.
- Property manager.
- AI concierge.
- Cleaning provider.
- Payment workflow.

## User Stories

- As a guest, I want clear departure instructions.
- As a host, I want late checkout requests approved only under property rules.
- As operations, I want unresolved issues visible before completion.

## Functional Requirements

- Provide checkout instructions using approved property knowledge.
- Track scheduled checkout, actual checkout, and departure confirmation.
- Capture late checkout requests and approval outcome.
- Surface unresolved service requests and outstanding payment considerations.
- Determine post-stay communication eligibility.

## Non-Functional Requirements

- Checkout status should update operations quickly.
- Late checkout decisions must be auditable.
- Post-stay messages must respect consent and WhatsApp policy.

## Business Rules

- Late checkout must not be automatically approved.
- Late checkout approval follows property or host business rules.
- Reservation should not be completed while critical service requests remain unresolved unless manually overridden.
- Review requests require communication eligibility.

## Validation Rules

- Checkout cannot precede check-in unless corrected manually.
- Late checkout request must specify requested time.
- Approval actor and timestamp are required for approved late checkout.

## Error Handling

- Unapproved late checkout should trigger host review or denial messaging.
- Missing departure confirmation may keep state at Check-Out Pending.
- Outstanding payment concerns should be surfaced but not resolved by AI unless approved workflow exists.

## Security Considerations

Checkout instructions may include access or key return details and must be limited to eligible guests.

## Privacy Considerations

Post-stay communication should use minimal data and respect opt-out.

## Multi-Tenant Considerations

Checkout actions must validate reservation and property company scope.

## AI Considerations

AI may explain checkout instructions and collect late checkout requests. AI must not approve late checkout or payment exceptions.

## Edge Cases

- Guest leaves without confirmation.
- Late checkout conflicts with next reservation.
- Service request remains unresolved.
- Payment dispute exists at checkout.

## Future Enhancements

- Cleaning handoff automation.
- Review request scheduling.
- Lost-and-found workflow.

## Acceptance Criteria

- Late checkout approval boundary is documented.
- Unresolved service and payment considerations are covered.
- Post-stay communication eligibility is defined.
