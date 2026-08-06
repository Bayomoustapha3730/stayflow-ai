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

The backend includes JWT login, refresh token rotation, password reset, email verification, secure password hashing, account lockout, authenticated password change, user profile preferences, session revocation, and invitation response handling.

## Implementation Guidance

- Never store plaintext passwords.
- Never store reset, verification, or refresh tokens in plaintext.
- Never return password hashes, reset tokens, or verification tokens in production responses.
- Use HTTPS in all deployed environments.
- Keep authentication errors generic for external clients.
- Preserve correlation IDs for troubleshooting.
- Revoke outstanding reset tokens when a new reset is requested.
- Revoke outstanding refresh-token sessions after successful password reset or password change.
- Apply rate limiting to password reset and verification resend workflows.
- Identity and profile changes should emit audit events without logging passwords or raw tokens.

## Future Work

Production email delivery, MFA, risk-based checks, and session management policies should be documented as they are implemented.
