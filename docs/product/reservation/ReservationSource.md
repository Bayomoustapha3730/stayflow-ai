# Reservation Source

## Executive Summary

Reservation Source defines where a reservation came from and how source-specific identifiers should be handled.

## Business Purpose

Source-aware tracking supports manual entry, controlled imports, reconciliation, duplicate detection, and future PMS integrations without making MVP depend on direct Airbnb or Booking.com API access.

## Scope

In scope: direct booking, Airbnb, Booking.com, Expedia, property manager import, manual reservation, API integration, and future PMS integrations.

Out of scope: direct platform API dependency for MVP and source-specific refund decisions.

## Actors

- Property manager.
- Host.
- Import operator.
- API integration.
- Future PMS integration.

## User Stories

- As a property manager, I want to import reservations from different sources.
- As a host, I want external references tracked by source.
- As support, I want to know which system is authoritative for changes.

## Functional Requirements

- Store reservation source and source-aware external reservation reference.
- Support manual entry and controlled import for MVP.
- Preserve external confirmation number when available.
- Track import timestamp and source metadata in future implementation.

## Non-Functional Requirements

- Source imports must be idempotent where deterministic identifiers exist.
- Source handling should be extensible.
- Source metadata must be auditable.

## Business Rules

- External confirmation numbers are not globally unique.
- MVP must not depend on direct Airbnb or Booking.com API access.
- External platform refund decisions remain authoritative when applicable.
- Manual reservations must remain supported.

## Validation Rules

- Source is required.
- External reference uniqueness must include Company and Source.
- Property and primary guest must be tenant-valid before import confirmation.

## Error Handling

- Duplicate source identifiers should update or flag according to import policy.
- Missing source reference can be allowed for manual reservations.
- Source conflicts create review outcomes.

## Security Considerations

Imported records must not bypass tenant validation or authorization.

## Privacy Considerations

Only necessary source metadata should be stored. Avoid importing unnecessary guest personal data.

## Multi-Tenant Considerations

Imports must map to a company before creating or updating reservations.

## AI Considerations

AI may use source only when it affects guest communication, such as explaining that a refund must be handled by the external platform. AI must not invent source policy.

## Edge Cases

- Same confirmation number from different sources.
- Manual reservation later imported from a platform.
- Source provides incomplete guest data.
- External source sends stale update.

## Future Enhancements

- PMS integration ADR and adapters.
- Source reconciliation dashboard.
- Import confidence scoring.

## Acceptance Criteria

- Supported sources are documented.
- MVP source limitations are explicit.
- Source-aware duplicate behavior is defined.
