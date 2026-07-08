# Guest Product Documentation

## Executive Summary

The Guest domain defines how StayFlow AI identifies, manages, protects, and uses guest information for WhatsApp concierge workflows. Guest records are company-scoped for the MVP, support international phone numbers and WhatsApp identifiers, and provide controlled context for reservations, conversations, preferences, privacy, and AI personalization.

## Business Purpose

Guest documentation aligns product, engineering, AI, security, and operations teams around one shared model for guest identity and guest-related workflows. It supports the product requirements in [Functional Requirements](../FunctionalRequirements.md), quality attributes in [Non-Functional Requirements](../NonFunctionalRequirements.md), and the multi-tenant direction in [ADR-0006](../../decisions/ADR-0006-use-multi-tenant-saas-design.md).

## Scope

This documentation covers guest profiles, lifecycle, preferences, communication history, stay history, privacy, AI context, duplicate detection, merge review, consent, and acceptance criteria. It does not implement backend models, APIs, migrations, or authentication.

## Actors

- Guest.
- Airbnb host.
- Property manager.
- Company administrator.
- Guest support agent.
- AI concierge workflow.
- WhatsApp Cloud API integration.

## Document Index

- [Guest Overview](GuestOverview.md)
- [Guest Lifecycle](GuestLifecycle.md)
- [Guest Profile](GuestProfile.md)
- [Guest Preferences](GuestPreferences.md)
- [Guest Communication](GuestCommunication.md)
- [Guest Stay History](GuestStayHistory.md)
- [Guest Privacy](GuestPrivacy.md)
- [Guest AI Context](GuestAIContext.md)
- [Acceptance Criteria](AcceptanceCriteria.md)

## User Stories

- As a host, I want returning guests recognized within my company so my team can provide better service.
- As a guest, I want StayFlow AI to remember explicit preferences without treating sensitive personal details as general memory.
- As a property manager, I want guest records associated with reservations so property context stays accurate.
- As an administrator, I want duplicate guests flagged safely without risky automatic merges.

## Functional Requirements

- Maintain company-scoped guest profiles.
- Support normalized international phone numbers and WhatsApp phone number identification.
- Track preferred language, email address, country of residence, notes, preferences, consent, communication history, and stay history.
- Associate guests with reservations and properties through reservations.
- Support duplicate detection, returning guest identification, and profile merge review.
- Provide privacy-safe guest context to AI workflows.

## Non-Functional Requirements

- Preserve tenant isolation and company-scoped access.
- Apply context minimization for AI.
- Support auditability for consent, merges, preference changes, and privacy actions.
- Use terminology consistent with [Naming Conventions](../../developer/NamingConventions.md) and [Entity Framework Guidelines](../../developer/EntityFrameworkGuidelines.md).

## Business Rules

- Guest identity is company-scoped for the MVP unless a future ADR changes the tenancy model.
- A Company A user must never access Company B guest data.
- AI-derived observations must not automatically become permanent guest preferences without an explicit business rule and appropriate consent.
- Guest-to-property association should be derived through reservations for stay-specific workflows.

## Validation Rules

- Normalized phone number, WhatsApp identifier, or email should be used for deterministic matching when available.
- Name similarity alone must not trigger automatic profile merging.
- Preferred language should use a supported language code or product-approved language label.
- Consent status must be explicit before using guest data for optional personalization.

## Error Handling

- Missing identifiers should create a partial guest candidate only when product rules allow.
- Duplicate conflicts should be flagged for review rather than hidden.
- Cross-company access attempts must be rejected and logged.
- Ambiguous AI context should trigger clarification or escalation.

## Security Considerations

Guest records contain personal data and must be protected through authorization, audit logging, and tenant-scoped queries. Future implementation must follow [Logging Standards](../../developer/LoggingStandards.md), [Error Handling](../../developer/ErrorHandling.md), and [ADR-0006](../../decisions/ADR-0006-use-multi-tenant-saas-design.md).

## Privacy Considerations

Guest data should be minimized, retained only as needed, and separated into operational data, guest-provided preferences, conversation context, and AI-derived observations. Sensitive personal data must not be treated as general AI memory.

## Multi-Tenant Considerations

For the MVP, guest profiles are company-scoped. Platform-scoped identity resolution is out of scope unless a future ADR explicitly changes this.

## AI Considerations

AI workflows may use current reservation, current property, approved property knowledge, preferred language, explicit preferences, relevant current conversation context, and approved stay history information. AI must not receive unrestricted guest history or sensitive data by default. See [Guest AI Context](GuestAIContext.md) and [ADR-0003](../../decisions/ADR-0003-use-openai.md).

## Edge Cases

- Shared phone numbers.
- Multiple WhatsApp identifiers.
- Guests with multiple reservations.
- Imported reservations with incomplete guest data.
- Duplicate guest records inside one company.
- Guests who disable AI personalization.

## Future Enhancements

- Platform-scoped guest identity ADR.
- Self-service privacy portal.
- Advanced duplicate resolution workflow.
- Guest data export and deletion automation.

## Acceptance Criteria

The documentation set is complete when all linked documents define the required product behavior, include testable acceptance criteria, and cross-reference existing StayFlow AI documentation where relevant.

## Documentation Issues Identified

- [Functional Requirements](../FunctionalRequirements.md) says guests should link to conversations and properties. This task requires property association through reservations. This documentation treats reservation-based property association as the preferred Guest domain model, but the broader functional requirements should be updated later to remove ambiguity.
- The repository has no ADR deciding whether guest identity should ever become platform-scoped. This documentation keeps MVP guest identity company-scoped in alignment with [ADR-0006](../../decisions/ADR-0006-use-multi-tenant-saas-design.md).
