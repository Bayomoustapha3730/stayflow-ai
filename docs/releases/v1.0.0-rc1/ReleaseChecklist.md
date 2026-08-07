# Release Checklist

- [x] Backend Release build succeeded.
- [x] Backend Release test suite passed.
- [x] Frontend typecheck passed.
- [x] Frontend test suite passed.
- [x] Frontend production build passed.
- [x] EF Core migration history was verified.
- [x] Database update completed without pending migrations.
- [x] Smoke test coverage exists for frontend health and backend readiness.
- [x] Release helper scripts were added under `scripts/release/`.
- [x] Backend and frontend security scans completed with no known vulnerabilities.
- [ ] Staging deployment verification has not been executed from this workspace.
- [ ] GitHub CLI review checks are unavailable in this container because `gh` is not installed.
