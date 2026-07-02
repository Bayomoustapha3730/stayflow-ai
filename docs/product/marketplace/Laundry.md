# Laundry

## Business Purpose

Laundry services support both guest convenience and host operations. Guests may need personal laundry during longer stays, while hosts need reliable linen and towel handling between reservations.

## User Stories

- As a guest, I want to request laundry pickup and delivery through WhatsApp.
- As a host, I want linen laundry tracked for each property.
- As a laundry provider, I want clear pickup details, item count, service type, and delivery expectations.

## Functional Requirements

- Capture pickup location, delivery location, service type, item count, weight estimate, date, time window, special care instructions, and price.
- Support guest personal laundry, linens, towels, dry cleaning, ironing, and express service.
- Track request status, provider assignment, pickup confirmation, delivery confirmation, and disputes.
- Link laundry requests to guest, property, reservation, provider, and conversation.

## Non-Functional Requirements

- Guest item details and delivery locations must be protected.
- Turnaround expectations must be clear.
- Service records should support dispute resolution.
- The flow should tolerate partial item counts at request time.

## Validation Rules

- Pickup location and requested date are required.
- Service type is required.
- Price quote must include currency when captured.
- Provider must be active and approved before assignment.
- Completion should include delivery confirmation.

## Edge Cases

- Item count changes after pickup.
- Guest requests urgent turnaround.
- Clothing is damaged or missing.
- Provider cannot deliver before checkout.
- Guest is unavailable for pickup or delivery.

## Acceptance Criteria

- Laundry documentation defines guest and host use cases.
- Pickup, delivery, provider assignment, and dispute handling are covered.
- Edge cases address damaged, missing, delayed, or changed items.

## Future Enhancements

- Itemized digital laundry receipts.
- Photo evidence at pickup and delivery.
- Automated express pricing.
- Linen inventory tracking.
