# Rollback Plan

If the release candidate must be reversed after deployment:

1. Roll back the container app image with `scripts/rollback-container-app.sh <resource-group> <container-app-name> <previous-image-ref>`.
2. Re-deploy the previous frontend and backend image pair.
3. Verify health endpoints with `tests/deployment/smoke.sh` or `scripts/release/verify-staging.sh`.
4. If schema rollback is required, restore the database from the pre-deployment backup and re-run the previous known-good migration set.

Operational notes:

- `scripts/apply-migrations.sh` requires `PRODUCTION_MIGRATION_APPROVED=true` and a valid `DATABASE_URL`.
- `scripts/generate-migration-script.sh` can produce an idempotent SQL script for review before applying changes.
