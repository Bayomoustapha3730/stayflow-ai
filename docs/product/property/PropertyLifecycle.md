# Property Lifecycle

## Purpose

This document describes the expected lifecycle of a property in StayFlow AI.

## Lifecycle States

### Draft

The property has been started but does not yet have enough information for guest-facing use.

Typical indicators:

- Missing required profile fields.
- No amenities or house rules.
- No knowledge articles.

### Active

The property is available for operational workflows and AI concierge use.

Requirements:

- Required property profile fields are complete.
- Guest-facing sections are reviewed.
- Company scope is valid.
- Property is marked active.

### Needs Review

The property is active but contains information that may require updates.

Examples:

- Outdated emergency contacts.
- Missing check-in instructions.
- Sparse knowledge articles.
- Conflicting house rules.

### Inactive

The property remains part of the company portfolio but is not available for operational or guest-facing workflows.

Expected behavior:

- The property may remain retrievable by authorized management workflows.
- Guest-facing automation should not use inactive property content.
- Audit history remains available.

`IsActive` represents operational availability. It must not be used as the deletion flag.

### Deleted

The property has been removed from normal management workflows while business history is preserved.

Expected behavior:

- `IsDeleted` is set to `true`.
- `DeletedAt` records the deletion timestamp.
- `DeletedBy` records the authenticated user when available.
- Normal property queries exclude deleted properties.
- Nested property data remains associated with the deleted property for historical integrity.

## Lifecycle Events

- Property created.
- Property profile updated.
- Nested content replaced or updated.
- Property activated.
- Property deactivated.
- Property soft deleted.

## Product Requirements

- Lifecycle changes should be auditable.
- Operators should understand why a property is unavailable.
- Future UI should make readiness and missing sections visible.
