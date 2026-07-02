# OWASP

## Purpose

OWASP guidance helps StayFlow AI avoid common web application security risks.

## Relevant Risk Areas

- Broken access control.
- Cryptographic failures.
- Injection.
- Insecure design.
- Security misconfiguration.
- Vulnerable and outdated components.
- Identification and authentication failures.
- Software and data integrity failures.
- Security logging and monitoring failures.
- Server-side request forgery.

## Engineering Guidance

- Validate all API inputs.
- Enforce authorization server-side.
- Use parameterized database access through EF Core.
- Keep dependencies updated and scanned.
- Avoid verbose production errors.
- Protect secrets and environment configuration.
- Log security-relevant events safely.

## Review Expectations

Pull requests that affect authentication, authorization, tenant isolation, payments, webhooks, or AI data handling should be reviewed against OWASP risk categories.
