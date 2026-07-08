# Guest Privacy

## Executive Summary

Guest Privacy defines how StayFlow AI should collect, use, retain, and protect guest data. It establishes privacy-by-design principles for guest profiles, preferences, communication history, stay history, consent, AI personalization, and retention.

## Business Purpose

Privacy is essential to trust. Hosts need useful context, guests need respectful handling of personal data, and StayFlow AI needs clear rules before implementing guest workflows.

## Scope

In scope: consent management, opt-out, AI personalization controls, data retention, deletion or restriction requests, data minimization, profile merging privacy, and auditability.

Out of scope: legal advice, final regulatory interpretation, and implementation of privacy automation.

## Actors

- Guest.
- Company administrator.
- Host.
- Support agent.
- Security reviewer.
- AI concierge workflow.

## User Stories

- As a guest, I want control over optional AI personalization.
- As a host, I want to use guest data responsibly.
- As an administrator, I want consent and retention actions audited.
- As a support agent, I want clear privacy boundaries when responding to requests.

## Functional Requirements

- Track consent for messaging, optional personalization, and AI-assisted support where required.
- Support AI personalization disabled state.
- Support opt-out from non-essential communication.
- Document retention considerations for profiles, conversations, preferences, and stay history.
- Support future deletion, export, correction, and restriction workflows.

## Non-Functional Requirements

- Privacy controls must be auditable.
- Sensitive data must be minimized in prompts, logs, and notes.
- Retention policy should be configurable as the product matures.
- Privacy decisions must preserve company isolation.

## Business Rules

- Operational guest data may be used for service delivery.
- Optional personalization requires consent or another documented lawful basis.
- Guest-provided preferences must be separated from AI-derived observations.
- AI personalization disabled means AI should not use guest-level preferences or stay history beyond what is necessary for the active workflow.

## Validation Rules

- Consent records should include scope, source, status, timestamp, and actor where applicable.
- Opt-out must override non-essential outbound messaging.
- Privacy-sensitive actions require audit metadata.
- Retention actions must not delete records required for active operational or legal workflows without review.

## Error Handling

- Missing consent should disable optional personalization.
- Conflicting consent states should choose the most restrictive state until reviewed.
- Deletion requests during active stays should trigger review.
- Failed privacy workflow actions must be logged and surfaced.

## Security Considerations

Privacy workflows are security-sensitive. Access should be limited, logged, and protected from accidental cross-company actions.

## Privacy Considerations

Data categories should remain distinct: operational data, guest-provided preferences, conversation context, and AI-derived observations. Sensitive personal data must not be treated as general AI memory.

## Multi-Tenant Considerations

Privacy requests apply to company-scoped guest records in MVP. The product must not reveal whether another company has a matching guest identity.

## AI Considerations

AI context must follow minimization and purpose limitation. AI personalization disabled must prevent optional guest memory and preference use, while still allowing minimal active reservation context needed to answer service questions.

## Edge Cases

- Guest requests deletion during an active stay.
- Guest disables AI personalization but still asks for check-in help.
- Guest withdraws messaging consent after a conversation.
- Duplicate profiles have different consent states.
- Staff entered sensitive information in notes.

## Future Enhancements

- Privacy request portal.
- Consent history timeline.
- Automated retention schedules.
- Sensitive data detection in notes and messages.

## Acceptance Criteria

- Consent, retention, AI personalization, and opt-out are documented.
- Most restrictive consent behavior is defined for conflicts.
- Sensitive data is excluded from general AI memory.
- Privacy workflows remain company-scoped.
