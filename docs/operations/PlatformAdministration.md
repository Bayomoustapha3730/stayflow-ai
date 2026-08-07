# Platform Administration

## Overview

Sprint 20D introduces a production-grade SaaS operations control plane under `/api/platform-admin` and `/platform-admin`.

The platform administration surface is restricted to platform administrators and designed for support, customer success, and operations teams.

## Security Controls

- Authorization policy: `platform.admin` enforced at controller level.
- Permission gate: every endpoint also requires `platform.admin` permission.
- Tenant isolation: no tenant-scoped host endpoint is reused for cross-tenant operations.
- Auditing: every mutating operation writes immutable audit records.
- Impersonation: requires explicit authorization code and reason; start/end events are audited.
- Secret handling: system configuration and provider health endpoints never expose credentials.

## API Surface

Primary groups:

- Tenant administration
- Tenant lifecycle audit
- Organization health
- Usage analytics
- Feature flag administration
- Operations metrics
- Background job/webhook/queue/email monitoring
- Provider health and billing health
- Subscription synchronization and tenant repair
- Read-only diagnostics and system configuration
- Support impersonation and incidents

## Operational Notes

- Queue depth is reported as unknown (`-1`) for in-memory channels that do not expose counters.
- Provider health combines deterministic configuration checks with persisted operational signals.
- Manual tenant repair normalizes lifecycle status and refreshes subscription snapshots.

## Runbook

1. Open `/platform-admin`.
2. Confirm platform-admin permission on the signed-in user.
3. Review incidents and provider health before taking actions.
4. For tenant actions, document reason in ticketing and execute suspend/reactivate/archive/restore.
5. For support access, use impersonation start with explicit authorization code, then end session immediately after completion.
