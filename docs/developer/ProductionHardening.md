# Sprint 17 Production Hardening

## Exception And Error Contract

The API now uses centralized exception handling with ASP.NET Core `IExceptionHandler`.
All unhandled errors return RFC 7807 `application/problem+json` responses.

Status mappings:

- `400`: validation and bad-request failures
- `401`: unauthorized authentication failures
- `403`: forbidden authorization failures
- `404`: not found
- `409`: conflict
- `429`: rate limited
- `503`: required dependency unavailable
- `500`: unknown server failure

Problem details fields:

- `type`
- `title`
- `status`
- `detail`
- `instance`
- `traceId`
- `correlationId`
- `errorCode` (when bounded and safe)

## Correlation And Trace IDs

Inbound header: `X-Correlation-Id`

Rules:

- max length is 64 characters
- invalid/malformed IDs are ignored
- a new bounded correlation ID is generated when missing or invalid
- response includes `X-Correlation-Id`
- outbound HTTP requests propagate the correlation ID

## Logging Policy

Structured request logging includes:

- method
- path
- status code
- elapsed milliseconds
- trace ID
- correlation ID

Do not log:

- raw guest or host message bodies by default
- prompts or full model outputs
- bearer tokens, JWTs, API keys, passwords, webhook signatures, or other secrets

## Health Endpoints

- `GET /health/live`: process liveness only
- `GET /health/ready`: readiness checks (database + optional dependency checks)
- `GET /health`: aggregate health

Public responses are minimal and return only a `status` value.

## Rate Limiting Policies

Named policies:

- `public-auth`: strict auth endpoint policy, partitioned by IP and route
- `guest-chat`: token bucket policy for guest chat traffic, partitioned by conversation/reservation/user/IP
- `host-api`: authenticated host policy partitioned by company and user
- `ai-generation`: stricter policy for costly AI operations
- `whatsapp-webhook`: bounded webhook rate policy
- `health`: bounded health endpoint policy

429 responses include `Retry-After` when available and safe ProblemDetails payloads.

## Security Headers

The backend sets:

- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy`
- `Content-Security-Policy` with `frame-ancestors 'self'`
- `Strict-Transport-Security` outside development

## CORS

CORS is configured with explicit origins from `Cors:AllowedOrigins`.
Wildcard `*` is rejected when credentials are enabled.

## Request And Payload Limits

Global request body limits are configured via:

- `ProductionHardening:Security:MaximumRequestBodyBytes`

Additional limits:

- WhatsApp webhook endpoint explicit request-size cap
- SignalR maximum receive message size bound to 64KB

## Dependency Resilience

Current resilience controls:

- bounded HTTP timeout on external clients
- bounded retries for transient WhatsApp operations
- retry telemetry without sensitive payload logs
- cancellation propagation in external calls

## Load Test Smoke Script

A lightweight k6 script exists at `tests/load/k6-smoke.js`.

Example:

```bash
STAYFLOW_BASE_URL=http://localhost:5000 k6 run tests/load/k6-smoke.js
```

Optional host scenario:

```bash
STAYFLOW_BASE_URL=http://localhost:5000 STAYFLOW_HOST_TOKEN=<token> k6 run tests/load/k6-smoke.js
```

Notes:

- local/Codespace results are not production capacity guarantees
- no real credentials should be committed or used in scripts

## Incident Troubleshooting

1. Capture `traceId` and `correlationId` from failing responses.
2. Search structured logs by correlation and trace IDs.
3. Verify `/health/live` and `/health/ready`.
4. If dependencies fail, confirm deterministic fallback behavior for AI paths.
5. Validate that rate limiting is not masking a broader dependency outage.

## Provider Outage Behavior

When LLM or WhatsApp dependencies are degraded:

- readiness may become degraded/unhealthy based on required/optional dependency policy
- deterministic fallback behavior should keep critical guest/host workflows functional
- retries remain bounded to avoid retry storms

## Rollback Guidance

If hardening changes cause regressions:

1. disable non-critical policy toggles through configuration first
2. roll back deployment to the previous stable image/version
3. validate health endpoints and critical API flows before full traffic restoration

## Known Limitations

- external dependency checks are intentionally minimal and sanitized
- detailed dependency diagnostics are available only in server logs
- local load test coverage is smoke-level, not full capacity modeling
