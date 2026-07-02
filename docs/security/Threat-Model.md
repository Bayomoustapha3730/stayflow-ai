# Threat Model

## Purpose

The threat model identifies likely risks to StayFlow AI and guides security priorities.

## Assets

- Company and user accounts.
- Guest personal data.
- Property knowledge and operational instructions.
- Conversation history.
- Payment metadata.
- API credentials and provider secrets.
- Audit logs and operational data.

## Threats

- Account takeover.
- Broken tenant isolation.
- Unauthorized property or guest data access.
- Webhook spoofing.
- Token theft or refresh token reuse.
- Prompt injection or unsafe AI tool execution.
- Payment callback manipulation.
- Secret leakage through logs or source control.

## Mitigations

- Strong authentication and secure password hashing.
- Role and permission enforcement.
- Company-scoped queries and tests.
- Webhook validation.
- Token rotation and revocation.
- AI input/output guardrails.
- Structured audit logging.
- Secret management and dependency scanning.

## Review Cadence

Threat modeling should be revisited when new integrations, payment workflows, AI tools, authentication changes, or data-sharing features are introduced.
