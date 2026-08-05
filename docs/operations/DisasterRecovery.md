# Disaster Recovery

## Targets

These are initial recovery targets to validate and refine over time:

- RPO: 15 minutes
- RTO: 60 minutes

## Recovery Steps

### Database Restore

1. Restore the latest verified backup or point-in-time snapshot.
2. Apply the reviewed idempotent migration script if needed.
3. Verify the readiness endpoint and key business flows.

### Container App Rollback

1. Identify the last healthy image SHA.
2. Redeploy the backend and frontend Container Apps to that image.
3. Confirm backend and frontend health.

### Secret Recovery

1. Restore missing secrets from Key Vault backup or source-of-truth secret inventory.
2. Rotate affected credentials.
3. Redeploy the affected revision.

### Full Environment Recreate

1. Re-deploy Bicep infrastructure.
2. Reapply secrets and identities.
3. Restore the database.
4. Deploy the previously validated image SHAs.

## Verification Checklist

- backend health passes
- frontend loads
- database connectivity is healthy
- SignalR negotiates
- telemetry resumes
- security headers still present
