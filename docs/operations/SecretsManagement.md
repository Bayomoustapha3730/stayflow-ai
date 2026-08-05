# Secrets Management

## Principles

- never commit production secrets
- use Key Vault or GitHub environment secrets
- rotate secrets regularly
- prefer managed identity where possible
- keep staging and production secret sets separate

## Typical Secrets

- database credentials or connection string
- JWT signing secret or certificate reference
- AI provider API key
- WhatsApp token and webhook secret
- email credentials
- SignalR connection string if used
- storage credentials if used

## Rotation Checklist

1. Add the new secret value.
2. Update the consuming deployment or app setting reference.
3. Deploy the new revision.
4. Verify health and smoke tests.
5. Remove the old value after the new version is stable.
