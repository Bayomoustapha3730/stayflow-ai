# Check-In

## Executive Summary

Check-In defines how a confirmed reservation becomes an occupied stay through standard, early, self, or host-assisted check-in.

## Business Purpose

Safe check-in protects guests, hosts, and property access information while reducing arrival friction and escalating failed access quickly.

## Scope

In scope: standard check-in, early check-in request, self check-in, host-assisted check-in, verification, failed check-in, access instruction delivery, and emergency escalation.

Out of scope: smart lock implementation and physical security operations.

## Actors

- Primary guest.
- Host.
- Property manager.
- AI concierge.
- WhatsApp workflow.
- Emergency contact.

## User Stories

- As a guest, I want access instructions when I am authorized to receive them.
- As a host, I want early check-in requests reviewed before approval.
- As support, I want failed check-ins escalated quickly.

## Functional Requirements

- Support standard, early, self, and host-assisted check-in.
- Verify reservation status, guest identity, property, allowed instruction release window, and Reservation Context Resolver outcome from [ADR-0007](../../decisions/ADR-0007-reservation-context-resolution.md).
- Track check-in verification status and actual check-in timestamp.
- Escalate failed check-in and emergency situations.

## Non-Functional Requirements

- Access instruction delivery must be reliable and auditable.
- Sensitive access information must be protected.
- Check-in workflows should respond quickly during arrival windows.

## Business Rules

- Door codes, lock codes, and sensitive access instructions must only be provided to verified eligible guests.
- Expired, cancelled, no-show, or unrelated reservations must not receive access instructions.
- Early check-in is never automatically approved unless property business rules explicitly permit it.
- Access authorization must be determined by deterministic application logic before AI context construction.

## Validation Rules

- Reservation must be Ready for Check-In or otherwise eligible by documented exception.
- Guest identifier must match the reservation's primary guest or approved contact.
- Property Company ID must match Reservation Company ID.
- Access release timing must be satisfied.

## Error Handling

- Failed identity verification blocks sensitive instructions.
- Failed access requires escalation to host or emergency workflow.
- Conflicting active reservations require clarification before instructions are sent.

## Security Considerations

Access credentials are sensitive. They must not be logged in plain text, shown to unrelated guests, or sent after reservation eligibility expires.

## Privacy Considerations

Check-in verification should not collect unnecessary personal data.

## Multi-Tenant Considerations

Check-in must validate company ownership across reservation, property, guest, and conversation.

## AI Considerations

AI may explain check-in steps using approved property knowledge but must not invent or expose access instructions outside authorization rules. AI must never decide whether access information may be released.

## Edge Cases

- Guest arrives early.
- Guest uses a different phone number.
- Lock code fails.
- Multiple active reservations match one phone number.
- Guest reports emergency access issue.

## Future Enhancements

- Smart lock integrations.
- Check-in verification automation.
- Access-code redaction controls.

## Acceptance Criteria

- Sensitive access instruction protection is documented.
- Early check-in approval rules are explicit.
- Failed check-in escalation is defined.
