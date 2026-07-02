# Authentication

## Purpose

Authentication verifies the identity of StayFlow AI users before they access company, property, guest, payment, or operational data.

## Principles

- Require strong password handling and secure credential storage.
- Use standardized login responses that do not leak whether an account exists.
- Apply account lockout for repeated failed attempts.
- Support email verification before granting full account access where appropriate.
- Keep authentication workflows observable through safe logs and audit events.

## Current Direction

The backend includes JWT login, refresh token support, password reset, email verification, secure password hashing, and account lockout groundwork.

## Implementation Guidance

- Never store plaintext passwords.
- Never return password hashes, reset tokens, or verification tokens in production responses.
- Use HTTPS in all deployed environments.
- Keep authentication errors generic for external clients.
- Preserve correlation IDs for troubleshooting.

## Future Work

Production email delivery, MFA, risk-based checks, and session management policies should be documented as they are implemented.
