# Authentication API

## Purpose

Authentication APIs manage user identity, session issuance, refresh tokens, password reset, email verification, roles, permissions, and current-user context.

## Current Endpoint Areas

- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/password-reset`
- `POST /auth/password-reset/confirm`
- `POST /auth/change-password`
- `POST /auth/email-verification`
- `POST /auth/email-verification/resend`
- `POST /auth/email-verification/confirm`
- `GET /auth/me`
- `PUT /auth/me`
- `GET /auth/sessions`
- `POST /auth/sessions/{sessionId}/revoke`
- `POST /auth/sessions/revoke-all`
- `GET /roles`
- `POST /roles`
- `POST /roles/{roleId}/permissions`

Organization invitation endpoints also support self-service response handling through:

- `POST /api/organization/invitations/accept`
- `POST /api/organization/invitations/reject`

## Response Format

Authentication endpoints should return standardized `ApiResponse<T>` objects with a correlation ID, success flag, message, data payload, and errors collection.

## Security Notes

- Passwords must never be returned by API responses.
- Refresh tokens should be rotated and stored securely.
- Authentication failures should not reveal whether an email address exists.
- Account lockout and permission checks should be observable through safe audit logs.

## Self-Service Notes

- `PUT /auth/me` updates the authenticated user's profile plus `preferredLanguage`, `timeZone`, and notification preferences.
- `POST /auth/change-password` requires the current password, enforces the password policy, and revokes outstanding refresh-token sessions.
- `POST /auth/password-reset` returns a generic success response whether or not the email exists.
- `POST /auth/password-reset/confirm` invalidates the one-time token after success and revokes outstanding refresh-token sessions.
- `POST /auth/email-verification` and `POST /auth/email-verification/resend` create a new one-time verification token for unverified users.
- `GET /auth/sessions` lists active refresh-token sessions grouped by session identifier.
- Session revoke endpoints act on refresh-token sessions only; current access tokens remain valid until expiry.

## Delivery Notes

- Identity emails are routed through a provider abstraction.
- Supported providers are Development, SMTP, SendGrid-compatible HTTP delivery, and Azure Communication Services-compatible HTTP delivery.
- Reset and verification tokens are stored only as hashes; raw tokens are used only to compose the outgoing email link.

## Future Documentation

Add request and response examples for login, refresh token rotation, password reset, email verification, role management, and permission assignment.
