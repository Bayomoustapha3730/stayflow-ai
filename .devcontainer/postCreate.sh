#!/usr/bin/env bash

set -Eeuo pipefail

WORKSPACE="/workspace"
BACKEND="$WORKSPACE/backend"
FRONTEND="$WORKSPACE/frontend"
TEST_PROJECT="$WORKSPACE/tests/StayFlow.Api.Tests/StayFlow.Api.Tests.csproj"

echo "========================================"
echo " StayFlow post-create setup"
echo "========================================"

echo
echo "Installed versions:"
dotnet --version
node --version
npm --version

cd "$WORKSPACE"

echo
echo "Restoring local .NET tools..."
if [[ -f "$WORKSPACE/dotnet-tools.json" ]]; then
    dotnet tool restore
elif [[ -f "$WORKSPACE/.config/dotnet-tools.json" ]]; then
    dotnet tool restore
else
    echo "No local dotnet-tools.json file found."
fi

echo
echo "Restoring backend packages..."
dotnet restore "$BACKEND/backend.csproj"

if [[ -f "$TEST_PROJECT" ]]; then
    echo
    echo "Restoring test packages..."
    dotnet restore "$TEST_PROJECT"
fi

echo
echo "Installing frontend packages..."
cd "$FRONTEND"

if [[ -f package-lock.json ]]; then
    npm ci
else
    npm install
fi

echo
echo "Creating frontend environment configuration..."

if [[ -n "${CODESPACE_NAME:-}" ]]; then
    FORWARDING_DOMAIN="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
    API_URL="https://${CODESPACE_NAME}-5243.${FORWARDING_DOMAIN}"
else
    API_URL="http://localhost:5243"
fi

cat > "$FRONTEND/.env.local" <<EOF
VITE_STAYFLOW_API_URL=${API_URL}
VITE_DEMO_EMAIL=demo.user@stayflow.local
VITE_DEMO_GUEST_ID=44444444-4444-4444-4444-444444444444
VITE_DEMO_RESERVATION_ID=55555555-5555-5555-5555-555555555555
VITE_DEMO_PROPERTY_ID=22222222-2222-2222-2222-222222222222
EOF

echo
echo "Frontend API URL: $API_URL"
echo
echo "Post-create setup completed."