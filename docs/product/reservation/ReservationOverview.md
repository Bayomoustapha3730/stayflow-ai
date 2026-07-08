# Reservation Overview

## Executive Summary

The Reservation domain is the authoritative stay record that connects a company-managed property to a primary guest and optional additional guests for a defined date range.

## Business Purpose

Reservations provide the operational context required for guest identification, WhatsApp messaging, AI concierge support, property readiness, marketplace service coordination, payment follow-up, and analytics.

## Scope

In scope: reservation relationships, structured data, lifecycle, source-aware references, guest counts, stay phases, duplicate detection, tenant validation, and AI context boundaries.

Out of scope: direct Airbnb API dependency, third-party refund decisions, payment capture, and source-specific booking policy enforcement.

## Actors

- Company administrator.
- Host.
- Property manager.
- Primary guest.
- Additional guest.
- AI concierge.
- WhatsApp workflow.
- Marketplace workflow.

## User Stories

- As a property manager, I want a reservation to show who is staying, where, and when.
- As a guest, I want support to match my current reservation.
- As a host, I want manual and imported reservations handled consistently.
- As an AI workflow, I need reservation context before answering stay-specific questions.

## Functional Requirements

- Store Reservation ID, Company ID, Property ID, Primary Guest ID, external reservation reference, reservation source, confirmation number, dates, guest counts, status, currency, amount when available, special requests, notes, created date, and updated date.
- Connect reservations to property, guest, communication, marketplace services, payments, and analytics.
- Support manual reservation creation and controlled import.
- Flag potential duplicate reservations for review.

## Non-Functional Requirements

- Reservation reads must be company-scoped.
- Active reservation selection should be deterministic.
- Reservation records should support audit and reporting.
- Financial fields must be optional when unavailable.

## Business Rules

- Reservation belongs to exactly one company and one property.
- Reservation has one primary guest.
- Additional guests may be counted without permanent profiles.
- Reservation source affects reference uniqueness and import behavior.

## Validation Rules

- Check-out date/time must be after check-in date/time.
- Adult count and child count must be non-negative.
- Total guest count must be positive for confirmed stays.
- Company ID must match property and primary guest ownership.

## Error Handling

- Missing required stay dates prevents confirmation.
- Source reference conflicts create duplicate-review outcomes.
- Cross-tenant associations are rejected.
- Missing optional booking amount does not fail creation.

## Security Considerations

Reservation data exposes occupancy and guest identity. Access must be role-aware, tenant-scoped, and audited.

## Privacy Considerations

Only data necessary for stay operations should be stored. Additional guest information should be minimized.

## Multi-Tenant Considerations

Reservation, Property, and Primary Guest must share the same Company ID. Requests attempting cross-tenant associations must fail.

## AI Considerations

AI may use reservation status, stay phase, dates, property, and approved special requests when relevant. Internal notes and unrelated financial data are excluded by default.

## Edge Cases

- Imported reservation has no primary guest match.
- Guest has overlapping stays.
- External confirmation number is reused by different platforms.
- Manual reservation later matches an imported booking.

## Future Enhancements

- Reservation calendar conflict detection.
- PMS import reconciliation.
- Source-specific import adapters.
- Event history timeline.

## Acceptance Criteria

- Reservation is documented as the bridge between Property, Guest, AI, WhatsApp, Marketplace, Payments, and Analytics.
- Company-scoped ownership is explicit.
- Required and optional reservation data are distinguished.
