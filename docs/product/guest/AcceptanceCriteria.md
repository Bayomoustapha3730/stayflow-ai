# Acceptance Criteria

## Business Purpose

This document defines the product-level acceptance criteria for the Guest domain documentation and future implementation. It ensures guest features support host operations, guest trust, privacy, AI readiness, and scalable SaaS architecture.

## User Stories

- As a product owner, I want clear acceptance criteria so engineering can implement the Guest domain consistently.
- As a host, I want guest data to improve service delivery without creating operational risk.
- As a guest, I want my information handled accurately, privately, and respectfully.

## Functional Requirements

- Guest records must support profile, preferences, communication, stay history, privacy, and AI context.
- Guest records must support create, read, update, soft delete, search, and pagination in future API work.
- Guest records must be company isolated.
- Guest data must be auditable through created, updated, and deleted metadata.
- Guest context must be usable by WhatsApp concierge workflows.

## Non-Functional Requirements

- Guest lookup must support low-latency phone-number matching.
- Guest data must be protected by secure storage and access controls when authentication is implemented.
- Guest AI context must be minimized and deterministic.
- Documentation must remain maintainable as product and architecture evolve.

## Validation Rules

- Guest records require company ownership.
- Confirmed guest records require a name and at least one contact method.
- Phone numbers must be normalized before matching.
- Consent and opt-out states must be respected by communication workflows.
- Soft-deleted guests must be excluded from normal product workflows.

## Edge Cases

- Duplicate guest records exist in the same company.
- Guest has multiple phone numbers or uses a shared number.
- Guest has overlapping stays.
- Guest requests deletion while support or dispute workflows are open.
- AI context has conflicting profile, preference, or stay data.

## Acceptance Criteria

- `docs/product/guest` includes all required Guest domain documents.
- Each document includes business purpose, user stories, functional requirements, non-functional requirements, validation rules, edge cases, acceptance criteria, and future enhancements.
- Mermaid diagrams are included where they clarify lifecycle, data relationships, communication, privacy, or AI context.
- Product guidance supports future backend, API, database, security, and AI implementation tasks.
- No application source code is modified.

## Future Enhancements

- Convert acceptance criteria into tracked product backlog items.
- Add API-level acceptance criteria after Guest endpoints are designed.
- Add database-level acceptance criteria after the Guest data model is finalized.
- Add QA test scenarios for guest lifecycle, privacy, and AI context.
