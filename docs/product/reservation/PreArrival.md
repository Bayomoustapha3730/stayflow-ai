# Pre-Arrival

## Executive Summary

Pre-Arrival covers the operational window after confirmation and before check-in, when StayFlow AI prepares the guest, property, host, and AI context for the upcoming stay.

## Business Purpose

Pre-arrival workflows reduce check-in friction, confirm guest identity and language, verify communication eligibility, and ensure property readiness.

## Scope

In scope: guest identification, preferred language confirmation, WhatsApp eligibility, consent checks, arrival time collection, airport transfer offer, check-in instruction scheduling, property readiness, and special request review.

Out of scope: automatic approval of paid services, direct airline or platform integrations, and payment collection.

## Actors

- Primary guest.
- Host.
- Property manager.
- AI concierge.
- WhatsApp workflow.
- Marketplace provider.

## User Stories

- As a guest, I want arrival details and check-in expectations before I travel.
- As a host, I want property readiness reviewed before the guest arrives.
- As an AI workflow, I need consent and eligibility before sending proactive messages.

## Functional Requirements

- Confirm primary guest identity and preferred language.
- Check WhatsApp communication eligibility and consent.
- Collect arrival time when missing.
- Offer airport transfer when property or company policy allows.
- Schedule check-in instructions based on property rules.
- Review special requests and flag host approval needs.

## Non-Functional Requirements

- Pre-arrival automation must be idempotent.
- Message scheduling should respect WhatsApp policy and consent.
- Readiness state should be visible to operations users.

## Business Rules

- Automated workflows may collect arrival time and preferred language.
- Airport transfer offers may be automated if configured, but provider assignment or paid service confirmation may require approval.
- Check-in instruction delivery must respect safe release windows.
- Special requests requiring property exception must be host-approved.

## Validation Rules

- Reservation must be Confirmed or Pre-Arrival.
- Guest and property must share Company ID with reservation.
- WhatsApp proactive messaging requires eligibility.
- Access-sensitive instructions require authorization rules.

## Error Handling

- Missing guest identifier creates review task.
- Failed WhatsApp eligibility check blocks proactive messages.
- Property readiness failure creates host escalation.

## Security Considerations

Sensitive access instructions must not be sent before the allowed time or to unverified guests.

## Privacy Considerations

Pre-arrival data collection should ask only for details needed for arrival and service delivery.

## Multi-Tenant Considerations

Pre-arrival workflows must not use properties, guests, or conversations outside the reservation company.

## AI Considerations

AI may help collect arrival time, confirm language, and answer pre-arrival questions using approved property knowledge. AI must not approve exceptions or disclose access codes without authorization.

## Edge Cases

- Guest has not opted into WhatsApp.
- Arrival time changes repeatedly.
- Airport transfer request needs payment or provider approval.
- Property readiness fails close to arrival.

## Future Enhancements

- Automated readiness checklist.
- Arrival-time prediction from flight data.
- Pre-arrival task board.

## Acceptance Criteria

- Pre-arrival workflows distinguish automated actions from host approval.
- Consent and WhatsApp eligibility are documented.
- Access instruction scheduling is protected.
