# Active Stay

## Executive Summary

Active Stay is the reservation phase where the guest is currently occupying or receiving support for the property.

## Business Purpose

The active reservation provides operational context for AI concierge answers, WhatsApp conversations, property knowledge retrieval, service requests, maintenance issues, emergency escalation, guest preferences, and host escalation.

## Scope

In scope: active reservation selection, AI context, service requests, maintenance, emergency escalation, guest preferences, host handoff, and multiple-reservation ambiguity.

Out of scope: implementing marketplace fulfillment or payment capture.

## Actors

- Primary guest.
- Additional guests.
- AI concierge.
- Host.
- Property manager.
- Marketplace provider.
- Maintenance provider.

## User Stories

- As a guest, I want StayFlow AI to answer based on the property I am staying in.
- As a host, I want active stay issues linked to the right reservation.
- As an AI workflow, I need a deterministic active reservation selection rule.

## Functional Requirements

- Identify active reservation by company, guest identifier, reservation dates, status, and property.
- Support ambiguity when one phone number maps to multiple active reservations.
- Link active stay conversations to service requests, maintenance, marketplace services, and escalations.
- Use approved guest preferences and property knowledge where allowed.

## Non-Functional Requirements

- Active reservation lookup must be fast for WhatsApp workflows.
- Ambiguous matches must fail safely.
- Active stay context must be auditable and tenant-scoped.

## Business Rules

- Do not assume a phone number maps to only one active reservation.
- If multiple active reservations match, AI should ask clarifying questions or escalate.
- Service requests should be tied to the reservation and property.
- Emergency issues should bypass normal automation and escalate.

## Validation Rules

- Active Stay requires eligible lifecycle status and current stay dates or verified occupancy.
- Guest, property, and reservation Company IDs must match.
- Service requests must reference the selected reservation or property.

## Error Handling

- Multiple matching reservations block automatic property-specific answers.
- Missing active reservation triggers clarification.
- Emergency classification triggers escalation.

## Security Considerations

Active stay context can include occupancy and access-sensitive information; restrict to authorized workflows.

## Privacy Considerations

AI and support workflows should only use current stay data needed to resolve the guest request.

## Multi-Tenant Considerations

Active stay selection must not search across companies.

## AI Considerations

AI may use reservation status, current property, approved property knowledge, preferred language, approved preferences, and relevant service requests. AI must not use unrelated stay history by default.

## Edge Cases

- One WhatsApp number is used for several guests.
- Guest has back-to-back reservations.
- Reservation dates changed but lifecycle did not update.
- Guest asks about another property.

## Future Enhancements

- Active reservation scoring.
- Multi-reservation disambiguation prompts.
- Real-time operations dashboard.

## Acceptance Criteria

- Multiple active reservations are handled safely.
- AI and service requests use selected reservation context.
- Emergency escalation is documented.
