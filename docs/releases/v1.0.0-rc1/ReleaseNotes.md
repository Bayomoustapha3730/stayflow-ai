# Release Notes

## Candidate

`v1.0.0-rc1`

## Scope

This release candidate consolidates the release-hardening work already present on the branch:

- Platform admin tenant, health, billing, diagnostics, and support impersonation endpoints.
- Frontend platform admin dashboard and API client coverage.
- Onboarding workflow stabilization and regression coverage.
- Deployment, health check, and migration automation for staging and production.

## Verification summary

- Backend Release build and test suite passed.
- Frontend typecheck, test suite, and production build passed.
- EF Core migration history and database update were clean.
- Existing smoke coverage verifies frontend and backend health endpoints plus authorization behavior.
