#!/usr/bin/env bash
set -euo pipefail

connection_string="${ConnectionStrings__DefaultConnection:-}"
if [[ -z "$connection_string" ]]; then
  printf '%s\n' "ConnectionStrings__DefaultConnection is not set." >&2
  # shellcheck disable=SC2317
  return 1 2>/dev/null || exit 1
fi

parse_connection_value() {
  local key="$1"
  local segment name value

  IFS=';' read -ra segments <<< "$connection_string"
  for segment in "${segments[@]}"; do
    name="${segment%%=*}"
    value="${segment#*=}"
    name="${name//[[:space:]]/}"
    if [[ "${name,,}" == "${key,,}" ]]; then
      printf '%s' "$value"
      return 0
    fi
  done

  return 1
}

PGHOST="$(parse_connection_value Host || true)"
PGPORT="$(parse_connection_value Port || true)"
PGDATABASE="$(parse_connection_value Database || true)"
PGUSER="$(parse_connection_value Username || parse_connection_value User\ Id || true)"
PGPASSWORD="$(parse_connection_value Password || true)"

export PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD

for variable in PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD; do
  if [[ -z "${!variable}" ]]; then
    printf '%s\n' "ConnectionStrings__DefaultConnection is missing a value for ${variable#PG}." >&2
    # shellcheck disable=SC2317
  return 1 2>/dev/null || exit 1
  fi
done

printf 'PostgreSQL environment exported for %s:%s/%s as %s.\n' "$PGHOST" "$PGPORT" "$PGDATABASE" "$PGUSER"