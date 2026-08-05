# Local Production Simulation

StayFlow can be simulated locally with production-style containers, the local PostgreSQL service, and runtime-injected frontend configuration.

## Build And Start

```bash
docker compose -f compose.production.yml --env-file .env.production up -d --build
```

## Health Checks

- Frontend: `http://localhost:8081/healthz`
- Backend live: `http://localhost:8080/health/live`
- Backend ready: `http://localhost:8080/health/ready`

## Database And Migrations

Generate the idempotent migration script:

```bash
./scripts/generate-migration-script.sh
```

Apply reviewed migrations after backup and approval:

```bash
PRODUCTION_MIGRATION_APPROVED=true DATABASE_URL="postgresql://..." ./scripts/apply-migrations.sh artifacts/migrations/stayflow-idempotent.sql
```

Validate migration history:

```bash
./scripts/verify-migrations.sh
```

## Runtime Configuration

The frontend image reads runtime config from `/config.js`, which is generated from container environment variables at startup.

- `STAYFLOW_API_URL`
- `STAYFLOW_SIGNALR_URL`
- `STAYFLOW_ENVIRONMENT`

## Shutdown

```bash
docker compose -f compose.production.yml down -v
```
