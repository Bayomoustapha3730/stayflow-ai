#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${DATABASE_URL:-}" ]]; then
  echo "DATABASE_URL must be set to apply migrations." >&2
  exit 1
fi

MIGRATION_SQL_FILE="${1:-}"
if [[ -z "$MIGRATION_SQL_FILE" ]]; then
  echo "Usage: apply-migrations.sh <idempotent-sql-file>" >&2
  exit 1
fi

if [[ ! -f "$MIGRATION_SQL_FILE" ]]; then
  echo "Migration script not found: $MIGRATION_SQL_FILE" >&2
  exit 1
fi

if [[ "${PRODUCTION_MIGRATION_APPROVED:-false}" != "true" ]]; then
  echo "Set PRODUCTION_MIGRATION_APPROVED=true after review and backup checkpoint." >&2
  exit 1
fi

psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$MIGRATION_SQL_FILE"

echo "Applied migrations from $MIGRATION_SQL_FILE"
