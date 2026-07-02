# JWT

## Purpose

JSON Web Tokens provide stateless access tokens for authenticated API requests.

## Guidelines

- Keep access token lifetimes short.
- Use refresh tokens for session continuation.
- Validate issuer, audience, signing key, expiration, and token integrity.
- Do not place secrets, private guest content, or sensitive operational data in token claims.
- Include only stable identity and authorization context required by the API.

## Claims

Recommended claim categories include:

- User identifier.
- Company identifier.
- Role or permission references.
- Token identifier when revocation tracking is needed.

## Security Notes

JWTs should be transmitted only over HTTPS. Clients must store tokens securely and avoid exposing them in logs, URLs, or analytics tools.

## Future Work

Document production signing-key rotation, token revocation strategy, and claim standards as authentication matures.
