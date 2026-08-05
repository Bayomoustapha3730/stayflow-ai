# Infrastructure

This directory contains Bicep infrastructure for Azure deployment.

## Intended Resources

- Azure Container Registry
- Log Analytics workspace
- Application Insights
- Container Apps environment
- backend Container App
- frontend Container App
- Key Vault
- PostgreSQL flexible server
- optional Azure SignalR Service
- optional storage account when a feature requires it

## Usage

Parameter files should be environment-specific and must not contain real secrets.

```bash
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/main.bicep \
  --parameters @infra/parameters/staging.json
```

## Notes

- Use immutable image SHAs, not `latest`.
- Keep staging and production parameter files separate.
- Store secrets in Key Vault or GitHub environment secrets, not in source control.
