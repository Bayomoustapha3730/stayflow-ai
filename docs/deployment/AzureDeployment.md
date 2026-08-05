# Azure Deployment

StayFlow targets Azure Container Apps with Azure Container Registry, Azure Database for PostgreSQL, Azure Key Vault, Log Analytics, and Application Insights.

## Architecture Summary

- Backend: ASP.NET Core container app with health probes at `/health/live` and `/health/ready`
- Frontend: static container app served by nginx with runtime config injection
- Database: PostgreSQL flexible server or the provider already used in the application
- Secrets: Key Vault-backed where supported
- Telemetry: Application Insights / Log Analytics

## Identity And Access

Use managed identity and GitHub OIDC for deployment. Avoid long-lived client secrets where the platform supports federation.

## Promotion Flow

1. Build immutable images from a commit SHA.
2. Deploy to staging.
3. Run migrations through the controlled migration script/job.
4. Run smoke tests.
5. Promote the exact same image SHA to production after approval.

## Required Environment Values

Backend examples:

- `ASPNETCORE_ENVIRONMENT`
- `ASPNETCORE_URLS`
- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Cors__AllowedOrigins__0`
- `ProductionHardening__...`

Frontend examples:

- `STAYFLOW_API_URL`
- `STAYFLOW_SIGNALR_URL`
- `STAYFLOW_ENVIRONMENT`

## Related Files

- [Local production simulation](LocalProduction.md)
- [Release process](../operations/ReleaseProcess.md)
- [Disaster recovery](../operations/DisasterRecovery.md)
