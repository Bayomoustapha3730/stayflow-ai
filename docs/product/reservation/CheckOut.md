# Check-Out

## Business Purpose

Check-out documentation defines how StayFlow AI supports a smooth departure, communicates property-specific instructions, captures stay completion signals, and prepares hosts for turnover.

## User Stories

- As a guest, I want clear checkout instructions before leaving.
- As a host, I want to know when the guest has checked out so cleaning and inspection can begin.
- As an operations user, I want checkout status connected to reservation completion.

## Functional Requirements

- Store scheduled checkout date, checkout time, actual checkout timestamp, status, and notes.
- Provide property-specific departure instructions, key return steps, waste disposal guidance, and late checkout policies.
- Support manual and guest-confirmed checkout.
- Link checkout to cleaning, maintenance, service request closure, and post-stay messaging.

## Non-Functional Requirements

- Checkout instructions must be property scoped and easy to update.
- Checkout state must be reliable for operational planning.
- Post-stay messages should respect guest consent and opt-out settings.

## Validation Rules

- Checkout must belong to an active or recently active reservation.
- Actual checkout timestamp should not precede actual check-in unless manually overridden.
- Late checkout must be approved or recorded with reason.
- Checkout completion should not erase reservation or communication history.

## Edge Cases

- Guest leaves without confirming checkout.
- Guest requests late checkout on the departure day.
- Guest damages property or leaves items behind.
- Cleaning team reports occupancy after checkout.
- Reservation is extended after checkout reminders were sent.

## Acceptance Criteria

- Checkout documentation supports departure communication and operational turnover.
- Late checkout and unconfirmed checkout scenarios are covered.
- Checkout state can update reservation lifecycle and downstream workflows.

## Future Enhancements

- Automated checkout reminders.
- Cleaning task integration.
- Lost-and-found workflow.
- Post-stay satisfaction survey.
