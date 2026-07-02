# Cleaning

## Business Purpose

Cleaning services help hosts maintain guest-ready properties between stays and respond quickly to mid-stay cleaning requests. This category supports operational reliability, guest satisfaction, and property quality control.

## User Stories

- As a host, I want to schedule cleaning after checkout.
- As a guest, I want to request mid-stay cleaning when available.
- As a cleaner, I want clear property access, timing, task scope, and completion expectations.

## Functional Requirements

- Capture property, reservation, requested date, preferred time window, cleaning type, task checklist, access notes, supplies required, and price.
- Support turnover cleaning, deep cleaning, emergency cleaning, and mid-stay cleaning.
- Track status from requested to scheduled, assigned, in progress, completed, cancelled, or disputed.
- Allow photo or checklist-based completion evidence in future implementation.
- Link cleaning requests to property, reservation, host, provider, and conversation.

## Non-Functional Requirements

- Cleaning schedules must avoid conflicts with guest occupancy and check-in windows.
- Access instructions must be protected.
- Completion status should update quickly for operations dashboards.
- Service quality should be measurable over time.

## Validation Rules

- Property and requested date are required.
- Cleaning type is required.
- Assigned provider must be active and approved.
- Access-sensitive information should only be shown to assigned providers.
- Completed cleaning should include timestamp and actor.

## Edge Cases

- Previous guest checks out late.
- Cleaner cannot access the property.
- New guest arrives before cleaning is complete.
- Property needs maintenance discovered during cleaning.
- Guest complains after completed cleaning.

## Acceptance Criteria

- Cleaning documentation supports turnover and guest-requested cleaning workflows.
- Access, timing, and service quality risks are covered.
- Cleaning can connect to reservation lifecycle and property operations.

## Future Enhancements

- Cleaner mobile checklist.
- Turnover automation from checkout.
- Inventory restock prompts.
- Cleaning quality scorecards.
