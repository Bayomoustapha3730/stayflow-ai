# Reservation Guests

## Executive Summary

Reservation Guests define the people associated with a reservation: one primary guest, optional additional adult guests, and child guest counts.

## Business Purpose

Guest structure supports stay operations while avoiding unnecessary personal data collection for every person in a booking.

## Scope

In scope: primary guest, additional adult guest counts, child guest counts, minimal additional guest data, and when a permanent Guest profile is appropriate.

Out of scope: collecting identity documents for all guests and platform-wide guest identity.

## Actors

- Primary guest.
- Additional guest.
- Host.
- Property manager.
- AI concierge.

## User Stories

- As a host, I want to know the guest count for readiness and rules.
- As a guest, I do not want unnecessary personal data collected for every companion.
- As an AI workflow, I need to know whether a question is from the primary guest or another approved contact.

## Functional Requirements

- Support one primary guest profile per reservation.
- Store adult count, child count, and total guest count.
- Allow minimal additional guest labels only when operationally needed.
- Avoid creating permanent Guest profiles for every additional guest unless a documented workflow requires it.

## Non-Functional Requirements

- Guest count data should support property capacity checks.
- Additional guest data must be minimized.
- Primary guest lookup must be efficient for WhatsApp workflows.

## Business Rules

- Primary guest is the main identity for reservation communication.
- Additional guests may be counted without permanent profiles.
- Additional guest profile creation requires business justification.
- Child guest records should avoid unnecessary personal identifiers.

## Validation Rules

- Primary Guest ID is required for confirmed reservations.
- Adult count and child count must be non-negative.
- Total guest count must not exceed property rules where such rules are configured.

## Error Handling

- Missing primary guest blocks confirmation.
- Excess guest count triggers validation or host review.
- Additional guest personal data collection beyond policy should be rejected or redacted.

## Security Considerations

Additional guest information must be access-controlled and minimized.

## Privacy Considerations

Children's information should be especially minimized. Do not collect names, ages, or identifiers unless a documented operational or legal workflow requires it.

## Multi-Tenant Considerations

Primary guest and reservation must share Company ID. Additional guest data inherits reservation company scope.

## AI Considerations

AI may use total guest count where relevant, such as house rules or capacity-sensitive services. AI must not expose other guest information.

## Edge Cases

- Primary guest is not the WhatsApp sender.
- Group booking has multiple adults.
- Child count affects service recommendations.
- Additional guest contacts support directly.

## Future Enhancements

- Approved additional contact workflow.
- Capacity validation by property.
- Group reservation support.

## Acceptance Criteria

- Primary guest and additional guest rules are documented.
- Minimum data collection is explicit.
- Permanent profiles are not created for every additional guest by default.
