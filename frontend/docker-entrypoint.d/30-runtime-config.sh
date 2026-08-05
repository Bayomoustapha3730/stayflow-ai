#!/bin/sh
set -eu

CONFIG_FILE="/usr/share/nginx/html/config.js"
API_URL="${STAYFLOW_API_URL:-http://backend:8080}"
SIGNALR_URL="${STAYFLOW_SIGNALR_URL:-$API_URL/hubs/conversations}"
ENVIRONMENT="${STAYFLOW_ENVIRONMENT:-production}"

cat > "$CONFIG_FILE" <<EOF
window.__STAYFLOW_RUNTIME_CONFIG__ = {
  apiUrl: "${API_URL}",
  signalRUrl: "${SIGNALR_URL}",
  environment: "${ENVIRONMENT}"
};
EOF
