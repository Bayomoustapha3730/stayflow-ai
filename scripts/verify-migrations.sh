#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet ef migrations list \
  --project "$ROOT_DIR/backend/backend.csproj" \
  --startup-project "$ROOT_DIR/backend/backend.csproj"

dotnet ef migrations script \
  --idempotent \
  --project "$ROOT_DIR/backend/backend.csproj" \
  --startup-project "$ROOT_DIR/backend/backend.csproj" \
  --output /tmp/stayflow-migrations.sql

wc -l /tmp/stayflow-migrations.sql
