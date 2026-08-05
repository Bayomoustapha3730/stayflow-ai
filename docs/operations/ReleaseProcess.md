# Release Process

## Versioning

Use semantic version tags for releases and immutable git SHA image tags for deployments.

## Flow

1. Merge to `main`.
2. Build and test in CI.
3. Deploy the tested SHA to staging.
4. Run smoke tests and verify telemetry.
5. Promote the same SHA to production after approval.

## Rollback Criteria

Rollback when any of these occur:

- readiness fails
- smoke tests fail
- error rate spikes materially
- migrations fail
- a container revision fails to stabilize

## Hotfixes

Hotfixes should reuse the same controlled process:

- commit
- build
- staging smoke test
- production approval
- immutable image promotion
