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

The property is hidden from active workflows through soft deletion or deactivation.

Expected behavior:

- The property does not appear in active lists.
- Guest-facing automation should not use inactive property content.
- Audit history remains available.

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
