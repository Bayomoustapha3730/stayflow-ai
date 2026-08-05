#!/usr/bin/env bash
set -euo pipefail

FRONTEND_URL="${STAYFLOW_FRONTEND_URL:-http://localhost:8081}"
BACKEND_URL="${STAYFLOW_BACKEND_URL:-http://localhost:8080}"

curl -fsS "$FRONTEND_URL/healthz" >/dev/null
curl -fsS "$BACKEND_URL/health/live" >/dev/null
curl -fsS "$BACKEND_URL/health/ready" >/dev/null

backend_headers="$(mktemp)"
curl -fsS -D "$backend_headers" "$BACKEND_URL/api/status" >/dev/null

if ! grep -qi '^x-correlation-id:' "$backend_headers"; then
  echo "Missing X-Correlation-Id response header" >&2
  exit 1
fi

unauthorized_headers="$(mktemp)"
unauthorized_body="$(mktemp)"
status_code="$(curl -sS -o "$unauthorized_body" -D "$unauthorized_headers" -w '%{http_code}' "$BACKEND_URL/conversations")"

if [[ "$status_code" != "401" && "$status_code" != "403" ]]; then
  echo "Expected unauthorized host endpoint to return 401 or 403, got $status_code" >&2
  exit 1
fi

if ! grep -qi 'content-type: application/problem+json' "$unauthorized_headers"; then
  echo "Unauthorized response should use ProblemDetails" >&2
  exit 1
fi

echo "Smoke tests passed"
