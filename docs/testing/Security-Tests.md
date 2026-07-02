# Security Tests

## Purpose

Security tests verify that StayFlow AI protects accounts, tenant data, secrets, webhooks, and sensitive workflows.

## Scope

Use security tests for:

- Authentication and login failure behavior.
- Role and permission enforcement.
- Company and tenant isolation.
- JWT validation and expiration.
- Refresh token rotation and reuse detection.
- Input validation and injection resistance.
- Webhook verification.
- Sensitive logging safeguards.

## Guidelines

- Include negative tests for unauthorized and cross-company access.
- Confirm API responses do not leak internal details.
- Validate that secrets and tokens are not logged.
- Test security-sensitive workflows after each major authentication or authorization change.
- Include dependency vulnerability scans in CI where practical.

## OWASP Alignment

Security tests should consider OWASP risk areas such as broken access control, authentication failures, injection, security misconfiguration, and logging or monitoring gaps.

## Future Work

Add automated security regression tests as authentication, WhatsApp, payment, and AI tool workflows mature.
