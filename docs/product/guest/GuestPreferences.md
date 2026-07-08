# Guest Preferences

## Executive Summary

Guest Preferences capture explicit, service-relevant choices provided by the guest or confirmed by authorized staff. Preferences improve hospitality and AI personalization, but they must remain separate from sensitive personal data and temporary conversation observations.

## Business Purpose

Structured preferences help hosts and property managers provide consistent service for repeat guests while giving StayFlow AI safe personalization inputs.

## Scope

In scope: preferred language, dietary preferences, airport transfer preferences, transportation preferences, check-in preferences, check-out preferences, communication preferences, accessibility preferences, and hospitality service preferences.

Out of scope: unverified AI assumptions, sensitive medical details, payment preferences, protected characteristics, and unrestricted conversation memory.

## Actors

- Guest.
- Host.
- Property manager.
- Support agent.
- AI concierge workflow.

## User Stories

- As a guest, I want my preferred language used in WhatsApp support.
- As a host, I want repeat service preferences remembered when guests consent.
- As a property manager, I want accessibility and transportation needs handled carefully.
- As an AI workflow, I need to distinguish explicit preferences from inferred observations.

## Functional Requirements

- Store structured preferences with category, value, source, confirmation status, consent basis, created date, and updated date.
- Support preference categories for language, dietary, airport transfer, transportation, check-in, check-out, communication, accessibility, and hospitality services.
- Allow guest-provided and staff-entered preferences.
- Flag AI-derived observations as non-permanent unless confirmed under business rules.
- Support preference update and removal.

## Non-Functional Requirements

- Preference lookup should be efficient for active reservations and AI context building.
- Preference changes must be auditable.
- Sensitive categories must be restricted and minimized.
- Preferences must be company-scoped.

## Business Rules

- Explicit guest preferences may be used for personalization when consent allows.
- AI-derived observations must not automatically become permanent preferences.
- Dietary and accessibility preferences may be sensitive and should be handled with extra care.
- Conversation context is not a preference until confirmed.
- A preference can be stay-specific or guest-level; the scope must be clear.

## Validation Rules

- Preference category is required.
- Preference source must be one of guest-provided, staff-entered, imported, or AI-derived observation.
- Permanent preferences require confirmation status.
- Sensitive preferences require consent or documented operational necessity.
- Free-text values should have length limits.

## Error Handling

- Unsupported preference category should return a validation error.
- Missing consent for optional personalization should prevent use in AI context.
- Conflicting preferences should be flagged for review.
- Failed preference update should not alter existing values.

## Security Considerations

Only authorized company users and approved workflows should create or edit preferences. Sensitive preference categories may require higher permissions.

## Privacy Considerations

Preferences must not become a dumping ground for sensitive personal data. Store only what is needed for hospitality service and consented personalization.

## Multi-Tenant Considerations

Preferences are tied to company-scoped guest profiles. Preference values must never be shared across companies.

## AI Considerations

AI may use explicit preferences such as preferred language or check-in preference. AI must treat inferred observations as temporary and lower confidence unless confirmed. Sensitive personal data must not be sent as general AI memory.

## Edge Cases

- Guest changes preferred language mid-conversation.
- Dietary preference includes allergy-like information.
- Accessibility preference is needed for a single stay only.
- AI infers airport transfer interest from a question.
- Preference conflicts with property rules.

## Future Enhancements

- Guest-facing preference confirmation.
- Preference expiration dates.
- Sensitivity labels by preference category.
- Preference import from booking platforms.

## Acceptance Criteria

- Structured preference categories are documented.
- Sources and confirmation status are required.
- AI-derived observations are separated from permanent preferences.
- Sensitive preferences have privacy and consent safeguards.
