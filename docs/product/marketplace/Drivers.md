# Drivers

## Business Purpose

Driver services help guests move between airports, properties, restaurants, attractions, and local errands while giving hosts a trusted way to recommend transport. This category is especially valuable for visitors unfamiliar with Nairobi, Mombasa, Kisumu, Diani, Naivasha, and other Kenyan destinations.

## User Stories

- As a guest, I want to request a reliable driver through WhatsApp.
- As a host, I want to recommend trusted transport without coordinating every trip manually.
- As a driver, I want clear pickup, destination, timing, and payment expectations.

## Functional Requirements

- Capture pickup location, drop-off location, date, time, passenger count, luggage needs, vehicle preference, and special instructions.
- Support airport transfers, local trips, day hire, and scheduled pickups.
- Show service status such as requested, quoted, accepted, assigned, in progress, completed, cancelled, and disputed.
- Allow host approval where company policy requires it.
- Link each driver request to guest, property, reservation, provider, and conversation.

## Non-Functional Requirements

- Driver requests must be time-sensitive and visible to operations users.
- Guest phone numbers and location details must be protected.
- Provider assignment should be auditable.
- The flow must support poor connectivity and late guest changes.

## Validation Rules

- Pickup date and time are required.
- Pickup location is required before provider assignment.
- Passenger count must be greater than zero.
- Price quote must include currency when captured.
- Provider must be active and approved before assignment.

## Edge Cases

- Flight is delayed.
- Guest changes destination after pickup.
- Driver cancels close to pickup time.
- Guest cannot identify the pickup point.
- Price dispute occurs after completion.

## Acceptance Criteria

- Driver documentation defines the data needed to request, assign, track, and complete transport.
- Safety and host approval expectations are clear.
- Edge cases cover delays, cancellations, and disputes.

## Future Enhancements

- Flight tracking integration.
- Driver live location sharing.
- Automated quote estimates.
- Preferred driver lists by property.
