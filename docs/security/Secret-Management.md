# Secret Management

## Purpose

Secret management protects credentials used by StayFlow AI and its integrations.

## Secret Examples

- JWT signing keys.
- Database credentials.
- WhatsApp Cloud API tokens.
- OpenAI API keys.
- Email provider credentials.
- Payment provider secrets.
- Webhook verification tokens.

## Rules

- Never commit secrets to source control.
- Use environment variables or managed secret stores.
- Rotate secrets when compromised or when personnel access changes.
- Scope credentials to the minimum required permissions.
- Keep separate secrets for local, staging, and production environments.

## Logging

Secrets must not appear in logs, exceptions, analytics events, screenshots, documentation, or support exports.

## Future Work

Document the selected production secret store, access review process, and rotation playbooks.
