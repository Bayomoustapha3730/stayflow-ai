# Multi-Factor Authentication

## Purpose

Multi-factor authentication adds an additional verification step beyond passwords to reduce account takeover risk.

## Candidate Factors

- Authenticator app one-time passwords.
- Email-based verification codes for lower-risk workflows.
- SMS-based verification where appropriate and supported.
- Recovery codes for account recovery.

## Recommended Scope

MFA should be prioritized for:

- Company owners.
- Administrators.
- Users with billing or payment permissions.
- Users who can access guest data at scale.

## Security Guidance

- Store MFA secrets encrypted.
- Provide recovery flows with strong verification.
- Log MFA enrollment, disablement, and failed attempts.
- Avoid using MFA bypasses without auditability.

## Current State

MFA is not implemented yet and should be introduced with a dedicated ADR or implementation plan.
