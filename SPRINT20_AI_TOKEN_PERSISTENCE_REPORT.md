# Sprint 20 — Phase 2B.3: Durable AI Token Telemetry

**Date:** 2026-10-26  
**Baseline:** `4d2108ee43cf9477d0ae206c87a7a762fbc56b31` (`main`, equal to `origin/main`)  
**Scope:** Durable provider-call telemetry only. No commit or push was made.

## Summary

Phase 2B.3 adds durable telemetry for calls made through `IAIProvider`. Each
provider call is recorded with its tenant, call identity, optional outer
operation, provider/model metadata, outcome, and token usage when known. The
telemetry store is isolated from the request's business-data context, and
telemetry failures do not replace a customer-facing result already obtained.

## Data model and migration

The new `AiProviderCallTelemetry` entity maps to
`AiProviderCallTelemetries` and includes:

- A GUID primary key and required `CompanyId` foreign key with `Restrict`
  deletion.
- A globally unique, required `ProviderCallId`.
- Optional paired `OuterOperationType` and `OuterOperationId` fields; no
  polymorphic foreign key.
- Optional bounded provider and model metadata.
- A `UsageKnown` flag and nullable input, output, and total token counts.
- A bounded string outcome: `Success`, `Unavailable`, or `Failed`.
- Inherited `CreatedAt` and `UpdatedAt` values.

Database checks enforce paired outer-operation fields and valid usage tuples:
unknown usage has all token counts null; known usage has nonnegative counts.
Indexes support provider-call identity, company/outer-operation lookup, and
creation-time queries.

Migration `20261026212835_AddAiProviderCallTelemetry` adds only the telemetry
table, its constraints and indexes, and the Company foreign key. It was
generated and inspected, but not applied. The model snapshot contains only the
new entity.

## Recording and idempotency

The store records the provider result or failure using the original
`ProviderCallId`, along with the `CompanyId` and outer identity supplied by
orchestration. Failed or unknown calls are recorded before fallback; fallback
calls do not create separate telemetry rows.

For a given call ID:

- Repeated unknown observations and identical known observations are
  idempotent.
- Unknown usage can be enriched with known usage; known usage cannot be
  downgraded.
- Conflicting known token counts or conflicting tenant, identity,
  provider/model, or outcome metadata produce a deterministic conflict without
  changing the stored row.
- A known zero-usage call is stored as `UsageKnown=true` with `0/0/0`.
  Unknown usage is stored as `UsageKnown=false` with null token counts.
- Different call IDs remain distinct. No row is created for a call that never
  occurred.

Writes use a serializable relational transaction, the database's global
unique constraint, handling for the expected PostgreSQL unique-index race,
and bounded retries for PostgreSQL serialization failures and deadlocks.
PostgreSQL tests cover concurrent unknown/known observations and conflicting
known observations. The database constraint remains authoritative.

Each write uses a fresh `ApplicationDbContext`, preventing telemetry saves
from flushing unrelated pending business changes in the request context.
Persistence errors are logged with call, tenant, and correlation IDs; they do
not replace an already obtained customer-facing result. Prompts, responses,
raw exceptions, credentials, and token secrets are not stored.

## Coverage and boundaries

- **Guest messages:** Telemetry uses `GuestMessage` and the persisted guest
  `ConversationMessage.Id`. Existing ChatService evidence verifies that this
  identity reaches the provider request.
- **Suggestions, generated replies, and workspace drafts:** Telemetry uses
  `CopilotOperation` and its durable operation ID. Tests cover all three roles;
  existing commercial-boundary tests verify that each service passes the
  durable ID.
- **Provider failures:** A thrown provider exception is recorded as `Failed`
  with unknown usage before fallback. It is not counted as zero usage.
- **`ConversationAIExchangeService`:** This registered service is currently
  unused and remains unclassified. No commercial outer identity or
  `AiRequests` admission was added. If invoked, a call can be recorded without
  an outer identity; it is not labeled as a guest message.
- **Grounded concierge:** This separate development-only language-model path
  does not use the `IAIProvider` call-ID/usage contract. No synthetic
  telemetry is created for it. Future external implementations need explicit
  observation and usage propagation; full paid-provider coverage is not
  claimed.
- **Provider metadata:** Metadata is stored only when supplied as real
  provider/model result metadata. Synthetic `TimedOut` or `Exception` wrapper
  names are not stored as provider names. The OpenAI adapter currently exposes
  its requested model, not a provider-resolved model.

## Unchanged behavior

Phase 2A `AiRequests` admission, quota keys, replay, exhaustion, provider
failure charging, and the legacy `/ai/orchestrate` 410 behavior were not
changed. This phase adds no `AiTokens` quota enforcement and no
`UsageRecord`/`UsageOperation` writes. Pricing, cost, currency, analytics UI,
and frontend sources are out of scope.

Telemetry deduplicates individual provider calls; it does not provide
exactly-once behavior for an outer operation. A new provider invocation gets a
new `ProviderCallId`, and telemetry deduplication does not suppress a whole
operation replay.

## Validation

- Persistence and orchestration-focused tests: **18 passed, 0 failed,
  0 skipped** before the final three commercial-role cases were added.
- Final Phase 2A/2B focused suite: **125 passed, 0 failed, 0 skipped**.
- PostgreSQL concurrency cases: **3 passed, 0 failed, 0 skipped**, against
  PostgreSQL 16 in a disposable `pg_virtualenv` cluster.
- Full backend: **1,148 passed, 0 failed, 0 skipped**, against a disposable
  PostgreSQL 16 cluster. The cluster and per-test databases were dropped.
- Frontend: **43 files and 262 tests passed**.
- Frontend typecheck: passed.
- Frontend production build: passed, with existing Rollup annotation and
  bundle-size warnings only.
- EF pending-model check after migration generation: passed; no additional
  pending model changes.
- `git diff --check`: passed.

The PostgreSQL race tests validate state transitions against a local disposable
database, not production load behavior. Repeat migration and concurrency
validation in the approved pre-deployment environment before applying the
migration. No migration was applied, and no persistent database,
`DefaultConnection`, or external provider was contacted.

## Files and handoff

Production changes include the telemetry entity and configuration, DbContext
registration, scoped store interface and implementation, and orchestration
wiring. Tests cover storage semantics, relational uniqueness, provider success
and failure/fallback persistence, commercial guest/Copilot identities, and
PostgreSQL concurrency. The migration and model snapshot were updated for the
telemetry entity.

This report is untracked. Existing audit and report Markdown files were not
modified. Nothing is staged, committed, or pushed.

## Phase 2B.3 correction follow-up — 2026-10-27

The final expected `ProviderCallId` unique-index race no longer exits by
rethrowing the database exception immediately after the last normal attempt.
After rolling back and disposing the failed transaction, the store opens a
fresh context/transaction, checks outer ownership, reloads the winning row,
and invokes the same `MergeObservation` policy used by normal replay. Compatible
replays return idempotently or enrich unknown usage; tenant, outer identity,
provider/model, outcome, and known-usage conflicts remain deterministic
telemetry conflicts. Non-matching unique violations are not reconciled.
Reconciliation stays bounded and retries PostgreSQL serialization failures
and deadlocks.

Direct store tests now cover same-call cross-tenant conflict with preservation
of the original row, changed outer ID and type, provider/model enrichment and
conflict, outcome replay and conflict, and unchanged audit timestamps on
idempotent replay. `RecordAsync_ReconcilesCompatibleObservationAfterFinalUniqueRaceAttempt`
forces three expected unique-race failures in the store's actual retry loop
and verifies the final clean-context reconciliation.

PostgreSQL coverage includes concurrent first insert,
unknown-to-known enrichment, conflicting known observations, and concurrent
cross-tenant reuse of one call ID. The controlled final-attempt test uses the
PostgreSQL provider and a test-only save interceptor to inject the expected
constraint race after a winner row exists; it verifies reconciliation rather
than a raw duplicate-key failure. The PostgreSQL test suite passed **5 tests,
0 failed, 0 skipped** on a disposable PostgreSQL 16 cluster.

Orchestration evidence now verifies that telemetry persistence failure is
logged and does not replace a successful provider response, and that known
usage has been persisted before a forced downstream output-validation failure.
Persistence remains best-effort; a failed telemetry write can leave an
observation absent. No outbox/retry queue was introduced. Grounded concierge
remains outside the `IAIProvider` call-ID/usage contract; complete paid-provider
or cost coverage is not claimed.

Validation after the correction:

- Backend build: passed, **0 errors**.
- Focused persistence/orchestration tests: **41 passed, 0 failed, 0 skipped**.
- Full backend on disposable PostgreSQL 16: **1,160 passed, 0 failed,
  0 skipped**.
- Frontend tests: **43 files, 262 tests passed**.
- Frontend typecheck: passed.
- Frontend production build: passed; existing Rollup annotation and
  chunk-size warnings only.
- EF pending-model check relative to the updated telemetry migration snapshot:
  passed; no pending model changes.
- `git diff --check`: passed.

All PostgreSQL validation used temporary clusters and test databases, which
were dropped after the runs. No migration was applied and no persistent
database, `DefaultConnection`, or external provider was contacted. The report
remains untracked; nothing is staged, committed, or pushed.
