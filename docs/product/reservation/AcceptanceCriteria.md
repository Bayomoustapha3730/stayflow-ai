# Acceptance Criteria

## Business Purpose

This document defines product-level acceptance criteria for the Reservation domain. It ensures the domain supports stay operations, guest communication, AI concierge context, pricing clarity, and future integrations.

## User Stories

- As a product owner, I want Reservation acceptance criteria so engineering can implement the domain consistently.
- As a host, I want reservations to drive check-in, checkout, cancellation, and extension workflows.
- As a guest, I want stay information to be accurate throughout the reservation lifecycle.

## Functional Requirements

- Reservation records must support company, property, guest, dates, status, source, pricing summary, lifecycle events, and operational notes.
- Reservation workflows must cover check-in, checkout, cancellation, extension, and pricing.
- Reservation context must be available to guest communication and AI concierge workflows.
- Reservation records must support search, filtering, pagination, soft delete, and audit metadata in future implementation.
- Reservation changes must be traceable to a source such as host, guest, platform, system, or AI-assisted workflow.

## Non-Functional Requirements

- Reservation data must be company isolated.
- Active reservation lookup must support low-latency WhatsApp responses.
- Lifecycle transitions must be deterministic and auditable.
- Pricing and payment-related information must avoid storing sensitive payment credentials.
- Reservation AI context must be privacy-safe and concise.

## Validation Rules

- Reservation must have company, property, guest, check-in date, and check-out date.
- Check-out date must be after check-in date.
- Guest count must be greater than zero.
- Cancellation reason is required for manual cancellation.
- Extension requests must have a requested checkout date later than the current checkout date.
- Currency is required when monetary amounts are present.

## Edge Cases

- Reservation is imported with missing guest details.
- Guest has overlapping reservations.
- External booking platform sends duplicate or stale updates.
- Reservation is cancelled after check-in.
- Extension is approved but payment fails.
- Pricing changes after guest confirmation.

## Acceptance Criteria

- `docs/product/reservation` includes all requested Reservation domain documents.
- The documentation covers overview, lifecycle, check-in, checkout, cancellation, extensions, pricing, and acceptance criteria.
- Mermaid diagrams are included where they clarify lifecycle, relationships, or workflow.
- The documentation is suitable for future backend, API, database, testing, and AI implementation work.
- No application source code is modified.

## Future Enhancements

- Convert acceptance criteria into backlog items.
- Add API-specific acceptance criteria when Reservation endpoints are designed.
- Add database acceptance criteria when Reservation entities and indexes are finalized.
- Add QA scenarios for lifecycle, extension, cancellation, and pricing workflows.
