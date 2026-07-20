#!/usr/bin/env bash

set -Eeuo pipefail

WORKSPACE="/workspace"
BACKEND="$WORKSPACE/backend"
FRONTEND="$WORKSPACE/frontend"

BACKEND_LOG="/tmp/stayflow-backend.log"
FRONTEND_LOG="/tmp/stayflow-frontend.log"

BACKEND_PID_FILE="/tmp/stayflow-backend.pid"
FRONTEND_PID_FILE="/tmp/stayflow-frontend.pid"

echo "========================================"
echo " Starting StayFlow development services"
echo "========================================"

wait_for_postgres() {
    echo "Waiting for PostgreSQL..."

    until pg_isready \
        -h postgres \
        -p 5432 \
        -U postgres \
        -d stayflow_ai_dev >/dev/null 2>&1
    do
        sleep 2
    done

    echo "PostgreSQL is ready."
}

create_frontend_environment() {
    if [[ -n "${CODESPACE_NAME:-}" ]]; then
        local forwarding_domain
        forwarding_domain="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"

        API_URL="https://${CODESPACE_NAME}-5243.${forwarding_domain}"
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

    echo "Frontend API URL: $API_URL"
}

stop_previous_process() {
    local pid_file="$1"
    local service_name="$2"

    if [[ ! -f "$pid_file" ]]; then
        return
    fi

    local pid
    pid="$(cat "$pid_file" 2>/dev/null || true)"

    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
        echo "Stopping previous $service_name process: $pid"
        kill "$pid" 2>/dev/null || true

        for _ in {1..10}; do
            if ! kill -0 "$pid" 2>/dev/null; then
                break
            fi

            sleep 1
        done

        if kill -0 "$pid" 2>/dev/null; then
            kill -9 "$pid" 2>/dev/null || true
        fi
    fi

    rm -f "$pid_file"
}

start_backend() {
    echo
    echo "Applying EF Core migrations..."

    cd "$BACKEND"

    ASPNETCORE_ENVIRONMENT=Development \
    dotnet ef database update

    stop_previous_process "$BACKEND_PID_FILE" "backend"

    echo
    echo "Starting backend on port 5243..."

    nohup env \
        ASPNETCORE_ENVIRONMENT=Development \
        dotnet watch \
            --project "$BACKEND/backend.csproj" \
            run \
            --urls http://0.0.0.0:5243 \
        >"$BACKEND_LOG" 2>&1 &

    echo $! > "$BACKEND_PID_FILE"
}

start_frontend() {
    stop_previous_process "$FRONTEND_PID_FILE" "frontend"

    echo
    echo "Starting frontend on port 5173..."

    cd "$FRONTEND"

    nohup npm run dev -- \
        --host 0.0.0.0 \
        --port 5173 \
        --strictPort \
        >"$FRONTEND_LOG" 2>&1 &

    echo $! > "$FRONTEND_PID_FILE"
}

wait_for_url() {
    local name="$1"
    local url="$2"
    local log_file="$3"

    echo "Waiting for $name..."

    for _ in {1..60}; do
        if curl --silent --fail "$url" >/dev/null 2>&1; then
            echo "$name is ready."
            return 0
        fi

        sleep 2
    done

    echo "$name did not become ready."
    echo
    echo "Last log lines:"
    tail -50 "$log_file" 2>/dev/null || true

    return 1
}

wait_for_postgres
create_frontend_environment
start_backend
start_frontend

wait_for_url \
    "StayFlow backend" \
    "http://localhost:5243/health" \
    "$BACKEND_LOG"

wait_for_url \
    "StayFlow frontend" \
    "http://localhost:5173/" \
    "$FRONTEND_LOG"

echo
echo "========================================"
echo " StayFlow development environment ready"
echo "========================================"
echo
echo "Frontend:"
echo "  http://localhost:5173"
echo
echo "Backend:"
echo "  http://localhost:5243"
echo
echo "Backend health:"
echo "  http://localhost:5243/health"
echo
echo "Demo login:"
echo "  Email:    demo.user@stayflow.local"
echo "  Password: ChangeMe123!"
echo
echo "Logs:"
echo "  Backend:  $BACKEND_LOG"
echo "  Frontend: $FRONTEND_LOG"
echostart_frontend() {
    stop_previous_process "$FRONTEND_PID_FILE" "frontend"

    echo
    echo "Preparing frontend..."

    cd "$FRONTEND"

    if [[ ! -d node_modules ]] || [[ ! -f node_modules/.package-lock.json ]]; then
        echo "Installing frontend dependencies..."

        if [[ -f package-lock.json ]]; then
            npm ci
        else
            npm install
        fi
    fi

    echo
    echo "Starting frontend on port 5173..."

    nohup npm run dev -- \
        --host 0.0.0.0 \
        --port 5173 \
        --strictPort \
        >"$FRONTEND_LOG" 2>&1 &

    echo $! > "$FRONTEND_PID_FILE"
}