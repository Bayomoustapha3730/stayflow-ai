# Acceptance Criteria

## Executive Summary

This document provides testable Given / When / Then acceptance criteria for the StayFlow AI Guest domain. It focuses on guest creation, returning guest identification, duplicate detection, preferences, consent, AI personalization, tenant isolation, retention, merge review, and preferred language behavior.

## Business Purpose

Acceptance criteria give product, engineering, QA, and security reviewers a shared definition of done before the Guest domain is implemented.

## Scope

In scope: product-level acceptance scenarios and business expectations. Out of scope: API schema, database migration details, and implementation-specific test fixtures.

## Actors

- Guest.
- Host.
- Company administrator.
- Support agent.
- AI concierge workflow.
- QA reviewer.

## User Stories

- As a product owner, I want Guest behavior defined in testable scenarios.
- As a host, I want guest recognition and preferences to work safely.
- As a guest, I want privacy, consent, and language preferences respected.
- As a security reviewer, I want tenant isolation explicitly tested.

## Functional Requirements

- Support guest creation, returning guest identification, duplicate detection, preference updates, consent management, AI personalization controls, tenant isolation, retention handling, merge review, and preferred language selection.

## Non-Functional Requirements

- Scenarios must be deterministic, auditable, and suitable for future unit, integration, security, and acceptance tests.
- Scenarios should align with [Testing Strategy](../../testing/TestingStrategy.md) and [Acceptance Testing](../../testing/AcceptanceTesting.md).

## Business Rules

- Guest profiles are company-scoped.
- Deterministic identifiers include normalized phone number, WhatsApp identifier, and email.
- Name similarity alone must never trigger automatic merging.
- AI-derived observations are not permanent preferences without explicit rule and consent.

## Validation Rules

- Phone numbers must be normalized before matching.
- Email must be validated when provided.
- Preferred language must be supported.
- Consent and personalization settings must be explicit and auditable.

## Error Handling

- Invalid input returns validation errors.
- Duplicate or ambiguous identities create review outcomes.
- Cross-company access is rejected.
- Missing consent disables optional personalization.

## Security Considerations

Acceptance tests must include cross-company access denial and authorization expectations when authentication is implemented.

## Privacy Considerations

Scenarios must verify data minimization, consent behavior, retention handling, and exclusion of sensitive data from AI context.

## Multi-Tenant Considerations

Every scenario involving lookup, merge, retention, or AI context must be evaluated within company scope.

## AI Considerations

AI scenarios must distinguish operational data, guest-provided preferences, conversation context, and AI-derived observations.

## Edge Cases

- Shared phone numbers.
- Duplicate email within a company.
- Same phone number across companies.
- Conflicting consent across duplicate records.
- Guest disables personalization during an active stay.

## Future Enhancements

- Convert scenarios into automated tests.
- Add API endpoint acceptance criteria.
- Add privacy request workflow tests.
- Add prompt-context regression tests.

## Acceptance Criteria

### New Guest Creation

```gherkin
Given a company user creates a guest with a name and normalized international phone number
When the guest is saved
Then the guest profile is created under that company
And the guest is not visible to other companies
And audit metadata is recorded
```

### Returning Guest Identification

```gherkin
Given a company has an existing guest with a normalized phone number
When a new reservation or WhatsApp message arrives with the same normalized phone number
Then StayFlow AI identifies the guest as a returning guest within that company
And does not check or expose guest records from other companies
```

### Duplicate Guest Detection

```gherkin
Given a company has an existing guest with a WhatsApp identifier
When a second guest profile is created with the same WhatsApp identifier
Then the system flags a deterministic duplicate
And prevents silent duplicate creation unless the merge policy allows it
```

### Name Similarity Does Not Merge

```gherkin
Given two guest profiles have similar names
When there is no matching normalized phone number, WhatsApp identifier, or email
Then the system must not automatically merge the profiles
And may flag them for manual review only
```

### Guest Preference Updates

```gherkin
Given a guest provides a check-in preference
When an authorized user records the preference
Then the preference is saved with category, source, confirmation status, and timestamp
And it is available for relevant future workflows within the same company
```

### Guest Consent

```gherkin
Given a guest has not granted optional personalization consent
When an AI context package is built
Then optional guest preferences and stay history are excluded
And only operational context required for the active workflow may be used
```

### AI Personalization Disabled

```gherkin
Given a guest has disabled AI personalization
When the guest asks a property question during an active stay
Then AI may use the current reservation, current property, and approved property knowledge
And must not use optional guest-level memory, historical preferences, or unrelated stay history
```

### Multi-Tenant Isolation

```gherkin
Given Company A and Company B both have guests with the same phone number
When a Company A user searches for that phone number
Then only Company A guest records are returned
And Company B guest data remains inaccessible
```

### Guest Data Retention

```gherkin
Given a guest profile is past the configured retention period
When retention processing is evaluated
Then the profile is flagged for retention action
And active reservations, legal holds, or unresolved disputes prevent automatic deletion
```

### Guest Profile Merge Review

```gherkin
Given two guest profiles share a deterministic identifier within the same company
When the duplicate detection process runs
Then the profiles are flagged for merge review
And merge execution requires documented policy approval and audit metadata
```

### Preferred Language Selection

```gherkin
Given a guest profile has preferred language set to Kiswahili
When the AI concierge prepares a response
Then the preferred language is included in the AI context if personalization and workflow rules allow
And the response should use the preferred language when supported
```
