# Staging Verification

Use the release helpers for staged deployments and smoke checks:

- `scripts/release/smoke-test.sh` runs the existing health and authorization smoke coverage.
- `scripts/release/verify-staging.sh` runs the smoke coverage and, when a bearer token is provided, probes a protected platform-admin endpoint.

## Environment variables

- `STAYFLOW_FRONTEND_URL` - frontend base URL, defaults to `http://localhost:8081`
- `STAYFLOW_BACKEND_URL` - backend base URL, defaults to `http://localhost:8080`
- `STAYFLOW_STAGING_BEARER_TOKEN` - bearer token for authenticated staging checks
- `STAYFLOW_STAGING_EXPECT_PLATFORM_ADMIN` - set to `true` to require the bearer token probe
- `STAYFLOW_PLATFORM_ADMIN_PATH` - protected endpoint to probe, defaults to `/api/platform-admin/system-configuration`

## Verification criteria

- Frontend health returns 200 at `/healthz`.
- Backend liveness and readiness return 200.
- The backend returns `X-Correlation-Id` on `/api/status`.
- Unauthorized host access returns 401 or 403 with ProblemDetails.
- Authenticated platform-admin checks return 200 when a valid bearer token is supplied.
