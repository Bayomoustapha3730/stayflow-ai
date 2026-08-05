#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_FILE="${1:-$ROOT_DIR/artifacts/migrations/stayflow-idempotent.sql}"

mkdir -p "$(dirname "$OUTPUT_FILE")"

dotnet tool restore

dotnet ef migrations script \
  --idempotent \
  --project "$ROOT_DIR/backend/backend.csproj" \
  --startup-project "$ROOT_DIR/backend/backend.csproj" \
  --output "$OUTPUT_FILE"

echo "Wrote idempotent migration script to $OUTPUT_FILE"
