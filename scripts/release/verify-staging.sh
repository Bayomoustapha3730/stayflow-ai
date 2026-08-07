#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND_URL="${STAYFLOW_BACKEND_URL:-http://localhost:8080}"
TOKEN="${STAYFLOW_STAGING_BEARER_TOKEN:-${STAYFLOW_SMOKE_BEARER_TOKEN:-}}"
EXPECTED_PLATFORM_ADMIN="${STAYFLOW_STAGING_EXPECT_PLATFORM_ADMIN:-false}"
ADMIN_PATH="${STAYFLOW_PLATFORM_ADMIN_PATH:-/api/platform-admin/system-configuration}"

"$ROOT_DIR/scripts/release/smoke-test.sh"

if [[ -z "$TOKEN" ]]; then
  if [[ "$EXPECTED_PLATFORM_ADMIN" == "true" ]]; then
    echo "STAYFLOW_STAGING_BEARER_TOKEN is required when STAYFLOW_STAGING_EXPECT_PLATFORM_ADMIN=true" >&2
    exit 1
  fi

  echo "Skipping authenticated staging probe because no bearer token was provided."
  exit 0
fi

response_headers="$(mktemp)"
response_body="$(mktemp)"
trap 'rm -f "$response_headers" "$response_body"' EXIT

status_code="$(curl -sS -o "$response_body" -D "$response_headers" -w '%{http_code}' \
  -H "Authorization: Bearer $TOKEN" \
  "$BACKEND_URL$ADMIN_PATH")"

if [[ "$status_code" != "200" ]]; then
  echo "Expected authenticated platform admin probe to return 200, got $status_code" >&2
  cat "$response_body" >&2
  exit 1
fi

if ! grep -qi '^content-type: application/json' "$response_headers"; then
  echo "Platform admin probe should return JSON" >&2
  exit 1
fi

echo "Authenticated staging probe passed"
