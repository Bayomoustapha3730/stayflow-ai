# Authentication API

## Purpose

Authentication APIs manage user identity, session issuance, refresh tokens, password reset, email verification, roles, permissions, and current-user context.

## Current Endpoint Areas

- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/password-reset`
- `POST /auth/password-reset/confirm`
- `POST /auth/email-verification/confirm`
- `GET /auth/me`
- `GET /roles`
- `POST /roles`
- `POST /roles/{roleId}/permissions`

## Response Format

Authentication endpoints should return standardized `ApiResponse<T>` objects with a correlation ID, success flag, message, data payload, and errors collection.

## Security Notes

- Passwords must never be returned by API responses.
- Refresh tokens should be rotated and stored securely.
- Authentication failures should not reveal whether an email address exists.
- Account lockout and permission checks should be observable through safe audit logs.

## Future Documentation

Add request and response examples for login, refresh token rotation, password reset, email verification, role management, and permission assignment.
