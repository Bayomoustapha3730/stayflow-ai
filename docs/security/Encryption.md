# Encryption

## Purpose

Encryption protects StayFlow AI data in transit and at rest.

## In Transit

- Require HTTPS for all production API traffic.
- Validate TLS certificates for provider integrations.
- Avoid transmitting tokens or credentials in URLs.
- Use secure webhook endpoints for WhatsApp and payment providers.

## At Rest

- Use managed database encryption where available.
- Encrypt sensitive secrets and provider credentials.
- Consider field-level encryption for highly sensitive data if required.
- Protect backups with encryption and access controls.

## Key Management

Keys should be stored in managed secret stores or key vaults, not in source control. Key rotation procedures should be documented before production launch.

## Future Work

Define encryption requirements for guest data, payment-related metadata, AI prompt logs, backups, and exported reports.
