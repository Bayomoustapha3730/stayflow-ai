# Guest Profile

## Executive Summary

The Guest Profile is the company-scoped operational record for identifying and supporting a guest. It stores contact details, preferred language, country of residence, notes, consent indicators, duplicate review status, and references to reservations and communication history.

## Business Purpose

Profiles help hosts and property managers recognize guests, support active stays, and reduce repeated questions while keeping guest information structured and governed.

## Scope

In scope: name, normalized phone number, WhatsApp identifier, email, preferred language, country of residence, guest notes, active status, consent summary, duplicate indicators, and merge review state.

Out of scope: payment credentials, government ID storage, medical records, and platform-wide identity.

## Actors

- Guest.
- Host.
- Property manager.
- Support agent.
- Company administrator.
- AI concierge workflow.

## User Stories

- As a host, I want reliable guest identity without searching chat threads.
- As a guest, I want my preferred language and contact details respected.
- As a support agent, I want notes that help service delivery without exposing sensitive data unnecessarily.
- As an administrator, I want duplicate guest records flagged for review.

## Functional Requirements

- Store operational guest fields: display name, normalized phone number, WhatsApp identifier, email, preferred language, country of residence, notes, active state, and audit fields.
- Support multiple reservations per guest.
- Support duplicate detection based on normalized phone number, WhatsApp identifier, and email.
- Support merge review status and merge audit metadata.
- Support returning guest identification inside the same company.

## Non-Functional Requirements

- Profile lookup by WhatsApp identifier and normalized phone number should be indexed in future data models.
- Updates must be auditable.
- Profile reads and writes must apply company scope.
- Notes should support moderation and retention policy.

## Business Rules

- Operational guest data supports service delivery.
- Guest-provided preferences belong in [Guest Preferences](GuestPreferences.md), not unstructured notes.
- Conversation context belongs in [Guest Communication](GuestCommunication.md), not permanent profile fields.
- AI-derived observations must not be written into the profile automatically.
- Name similarity alone must not merge profiles.

## Validation Rules

- Normalized phone number should follow international format when provided.
- WhatsApp identifier should be unique within company when available.
- Email should be unique within company when used as a deterministic identifier, subject to merge policy.
- Preferred language should use supported values.
- Guest notes should reject payment credentials, access codes, or sensitive personal data.

## Error Handling

- Duplicate deterministic identifiers should return a conflict or create a review task.
- Invalid phone number or email should return a validation error.
- Merge attempts with unresolved conflicts should be blocked.
- Cross-company profile access should return authorization failure without exposing record existence.

## Security Considerations

Guest profiles must be protected by authorization and tenant-scoped access. Merge actions should be privileged and logged.

## Privacy Considerations

Profiles should use data minimization. Sensitive personal data must not be stored as general notes. Retention and deletion rules should be applied to inactive or post-stay profiles.

## Multi-Tenant Considerations

Guest profiles are company-scoped for MVP. Deterministic identifiers may match across companies, but that must not create shared identity or cross-company visibility.

## AI Considerations

AI may use profile fields only when relevant: preferred language, active reservation, and explicit preferences are usually safer than free-form notes. Notes should not be sent to AI by default.

## Edge Cases

- Guest shares a phone number with a spouse or assistant.
- Guest has different WhatsApp and booking phone numbers.
- Guest changes email address.
- Booking import creates a duplicate profile.
- Support user enters sensitive data in notes.

## Future Enhancements

- Guest profile completeness score.
- Merge review workflow.
- Consent-aware profile export.
- Field-level sensitivity labels.

## Acceptance Criteria

- Profile fields are documented.
- Duplicate detection avoids name-only automatic merging.
- Company scope is explicit.
- Profile data categories are separated from preferences, communication context, and AI observations.
