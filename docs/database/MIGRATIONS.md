# Migrations

## Purpose

Entity Framework Core migrations manage database schema changes for StayFlow AI.

## Migration Rules

- Include migrations with any committed model or schema change.
- Review generated migrations before committing.
- Watch for destructive operations such as `DropTable`, `DropColumn`, or drop-and-recreate patterns.
- Prefer rename operations when preserving existing data.
- Keep old committed migration history stable.

## Local Workflow

1. Update EF Core models and configuration classes.
2. Generate a migration using the local EF Core tool.
3. Review the generated migration and model snapshot.
4. Build the backend.
5. Generate or review an idempotent migration script when needed.
6. Commit model changes, migrations, and related documentation together.

## Deployment Guidance

- Apply migrations through a controlled deployment process.
- Back up production data before high-risk migrations.
- Test migrations against staging before production.
- Document manual migration steps if a migration requires operational coordination.

## Rollback

Rollback strategy should be considered before applying production migrations. Some data transformations cannot be safely reversed without backup restore or custom repair scripts.
