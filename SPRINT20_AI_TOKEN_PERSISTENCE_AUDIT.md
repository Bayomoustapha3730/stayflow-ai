# Sprint 20 — Phase 2B.3 Durable AI Token Telemetry Architecture Audit

**Audit date:** 2026-10-26
**Repository:** `/workspace`
**Scope:** Read-only source/history audit. No application code, tests, schema, migration, or database was changed.

## Repository state and Phase 2B.2 prerequisite

| Check | Result |
|---|---|
| Branch | `main` |
| HEAD | `4d2108ee43cf9477d0ae206c87a7a762fbc56b31` |
| Upstream | `origin/main`, same SHA |
| HEAD equals upstream | Yes |
| Tracked worktree | Clean |
| Staged changes | None |
| Untracked files | Nine existing Sprint 20 Markdown reports, including this report |
| Phase 2B.2 committed | Yes |
| Safe to begin Phase 2B.3 design | Yes; do not implement until design is approved |

The recent history contains the Phase 2B.2 runtime aggregation work:

- `f4de25571f3405e98cebde84a5f88d91b08830da` — Add runtime AI token aggregation
- `c9becc02083a1b728dbe4f475aad61aa1d8ff0f1` — Add runtime AI token aggregation
- `74088741bf9a76bae9b03d9b4bc1e9ee38f4c66b` — Add runtime AI token aggregation
- `6deb4c09b80f7354f4f5b5555d41a8d47131e4c6` — Add direct known-zero token aggregation coverage
- `4d2108ee43cf9477d0ae206c87a7a762fbc56b31` — Clean up Phase 2B.2 audit artifacts

Phase 2B.2 is not an unresolved worktree change. No later migration or token-persistence implementation is present in the reviewed state.

## Current Phase 2B.2 runtime contract

The source was checked directly at the current HEAD.

- `ProviderCallId.New()` produces a StayFlow-generated GUID-N string. The value object rejects blank values, but its public constructor does not enforce GUID syntax or a maximum length.
- `AIReplyOrchestrator` creates a fresh provider-call ID immediately before each logical `IAIProvider.GenerateAsync` invocation. The request/result carry that ID through the current invocation. The ID is not durable and is not reused automatically across HTTP requests or process restarts.
- `AIProviderRequest` also creates an ID by default for direct callers.
- Commercial outer identity is represented by the pair `OuterOperationType` + `OuterOperationId`; current types are `GuestMessage` and `CopilotOperation`.
- The runtime aggregation is request-local. It is only created when both outer identity values are supplied; it is not persisted.
- `AiTokenUsage` contains separate `long` input, output, and total values. A null usage object means unknown; a non-null `0/0/0` tuple means known zero.
- Runtime aggregation counts every observed call ID, including calls with unknown usage. Zero observed calls has `ProviderCallCount = 0`; observed calls with unknown usage have a positive call count.
- Duplicate behavior is correct: unknown→unknown is idempotent; unknown→known enriches; known→same-known is idempotent; known→unknown does not downgrade; conflicting known values throw without replacing the first value.
- Aggregate sums use checked arithmetic and surface overflow as `InvalidOperationException`.
- Completeness is `Unknown` for no calls and for all-unknown calls, `Partial` for mixed known/unknown calls, and `Complete` only for at least one known call with no unknown calls. “Complete” only describes calls observed by the instrumented path; it is not proof of universal provider coverage.
- `AiTokenUsage` itself does not validate non-negative values or enforce `TotalTokens == InputTokens + OutputTokens`. A persistence schema should reject negative values but should store the provider-reported total independently rather than imposing the unproven equality.

## Commercial outer identities and replay behavior

| Flow | Current outer identity | Replay behavior evidenced by source | Possible second external provider call? |
|---|---|---|---|
| Guest Chat | `GuestMessage` / persisted `ConversationMessage.Id` | `ChatService` checks an existing `ExternalMessageId` before creating a new message; on a match it returns early without orchestration. Without that key, a retry can create a new persisted guest message and a different outer ID. The message is persisted before AI admission and orchestration. | A matching existing external ID normally does not invoke again. A request creating a new message can invoke again as a separate call. Concurrent same-key submissions are constrained by the database unique index and insertion path; this code does not establish provider-result replay or universal exactly-once execution. |
| Copilot Suggestions | `CopilotOperation` / supplied durable `CopilotOperation.Id` | Admission recognizes the existing operation and idempotently reuses the `AiRequests` quota key, but service code ignores the replay result and still calls orchestration. Operation status does not suppress execution or return a cached result. | Yes. If the repeated orchestration reaches an external provider, it is a new provider invocation and must have a new ProviderCallId and a separate row. |
| Copilot Generated Reply | `CopilotOperation` / supplied durable `CopilotOperation.Id` | Same admission/re-execution behavior as Suggestions. | Yes, under the same conditions. |
| Workspace Draft | `CopilotOperation` / supplied durable `CopilotOperation.Id` | The operation is admitted. When LLM wording is enabled, a replay calls orchestration again; when disabled, it produces deterministic wording without the shared provider call. | Yes when LLM wording is enabled and the orchestration actually invokes an external provider. |

`ProviderCallId` and `OuterOperationId` are separate semantic identities. Replaying an outer operation that starts a new provider invocation is not a replay of the same provider call. It must produce a new call ID and a new telemetry row. A telemetry-write retry for one already-executed provider call must reuse that call's original ID and must never call the provider again. Current code does not explicitly validate that their serialized values differ; callers should not use one as the other.

Exactly-once remote execution cannot be guaranteed by the current code. There is no durable provider response cache or provider-side idempotency contract. A process crash after dispatch may leave an uncertain call; a later new invocation is a distinct call, not an update to the old ID.

## AI invocation inventory

| Invocation/path | Classification | Current behavior and persistence-design implication |
|---|---|---|
| Guest Chat via `ChatService` → `AIReplyOrchestrator` | `COMMERCIAL_OUTER` | Has trusted tenant context and persisted guest-message identity. Uses `AiRequests` admission separately. The provider call can be recorded against the message ID. |
| Copilot Suggestions | `COMMERCIAL_OUTER` | Durable `CopilotOperation.Id` is available. Replays can invoke again and each actual invocation gets its own call row. |
| Copilot Generated Reply | `COMMERCIAL_OUTER` | Same durable Copilot-operation correlation. |
| Workspace Draft | `COMMERCIAL_OUTER` when LLM wording is enabled | Uses `CopilotOperation.Id`; deterministic mode does not call `IAIProvider`. |
| Copilot Retrieval Diagnostics | `INTERNAL_SUB_OPERATION` | The authenticated GET diagnostics endpoint calls `AIReplyOrchestrator` for suggested replies but supplies no outer-operation identity and performs no Copilot-operation admission. Do not invent an operation ID. Before claiming full commercial coverage, either explicitly keep this path outside paid generation or give it a deliberate durable operation/admission identity. Tenant identity is available. |
| `ConversationAIExchangeService` | `DORMANT_UNCLASSIFIED` | Registered in DI, but no source caller was found. If invoked, it stores a guest message and calls the shared orchestrator without outer identity or `AiRequests` admission. Do not infer `GuestMessage` identity from the guest-related message. |
| `GroundedConciergeResponseGenerator` | `DEVELOPMENT_NONCOMMERCIAL` | Uses a separate language-model interface. The current factory resolves `DevelopmentConciergeLanguageModel` in either configuration branch. It produces no token usage and has no `ProviderCallId`; do not fabricate a paid call or known-zero usage. Reclassify and instrument its provider boundary before substituting an external model. |
| Configured `DevelopmentAIProvider` | `DEVELOPMENT_NONCOMMERCIAL` | This is a real `IAIProvider` invocation when selected. It returns deterministic output, `ProviderName=Development`, a synthetic model/request ID, and no usage. If the recorder covers every `IAIProvider` invocation, record it as an observed call with unknown usage and development attribution, not as zero tokens or an external vendor call. |
| Legacy `AIOrchestrator` | `LEGACY_DEPRECATED` | Registered and still contains a direct provider invocation, but `AIOrchestrationController` returns HTTP 410. It passes no commercial outer identity. If reactivated, add explicit tenant/operation policy and telemetry before enabling it. |

The shared `IAIProvider` interface is used by the current orchestrator, the legacy orchestrator, and the diagnostics path. A provider-neutral recorder should cover the actual invocation boundary without adding EF/DbContext dependencies to `OpenAIAIProvider` or `OpenAIResponsesClient`.

## Provider, model, request ID, outcome, and failure metadata

- The configured `IAIProvider` is selected as OpenAI or Development in DI. `OpenAIAIProvider` identifies itself as `"OpenAI"` and has the configured `OpenAIOptions.Model` before dispatch.
- `OpenAIResponsesClient` currently places the requested model string into its `ModelName` response property; it does not map a separately verified provider-resolved model. Persist/label this value as the configured/requested model. Do not claim it is necessarily the model actually served by the provider.
- For an OpenAI SDK response, `ResponseResult.Id` is exposed as `RequestId`. It is optional response metadata, not a durable deduplication key. An exception may not expose a response ID.
- The Development provider returns synthetic model and request IDs. Do not label the synthetic request ID as an external provider request ID.
- `AIProviderOutcome` currently contains `Success`, `Unavailable`, and `Failed`. Timeout, cancellation, network, and provider errors are mapped to these outcomes plus coarse failure categories. The shared orchestrator may also catch exceptions and create synthetic labels; do not persist those labels as vendor names.
- A provider-call outcome is separate from the later outer-operation result. For example, a provider can succeed and then output validation or outer persistence can fail. `FallbackUsed` is not the provider-call outcome.
- Failure categories are short allowlisted strings (`Timeout`, `RateLimited`, `Authentication`, `InvalidRequest`, `ProviderServerError`, `Network`, `Cancelled`, `EmptyResponse`, `UnexpectedResponse`, `Unknown`). Persist only a controlled category. Do not persist raw exception text, prompts, provider payloads, or HTTP bodies/status detail by default.

## Recommended durable grain and deduplication

**Recommendation: one durable row per StayFlow-observable logical provider invocation.** The row represents one invocation of the provider abstraction, not one outer operation and not each hidden SDK/network retry. Store multiple rows for one outer operation when it causes multiple actual provider invocations.

This grain preserves per-call usage, outcome, provider/model attribution, failure, and replay evidence. It supports unknown→known enrichment and later operation-level aggregation without making a denormalized aggregate a second source of truth.

**Durable uniqueness:** a global unique constraint on StayFlow-owned `ProviderCallId`. The ID is generated per call, independent of tenant; global uniqueness detects accidental reuse across tenants rather than permitting two rows for what claims to be the same call. Every lookup and update must additionally include `CompanyId`; a call-ID conflict with a different company is an invariant/security error, never a cross-tenant update. `ProviderCallId` is currently a string value object with no enforced syntax/length, so the implementation must validate or align its DB max length before relying on its GUID-N generation contract.

| Incoming observation for the same ProviderCallId | Required durable behavior |
|---|---|
| First observation | Insert one row; pre-call state may be `Pending`. |
| Unknown usage replay | Idempotent no-op; never create a second row. |
| Unknown→known usage | Enrich the same row with the complete token tuple. |
| Known→same-known | Idempotent no-op. |
| Known→unknown | No downgrade; preserve known values. |
| Known X→known Y where X differs from Y | Deterministic conflict; preserve the first known tuple and surface/log the conflict. |
| Different ProviderCallId, same outer operation | Independent provider-call row. |
| Identity/tenant/outer metadata differs for same call ID | Reject as an invariant conflict; do not overwrite. |

**PostgreSQL concurrency:** use the unique call-ID index and a short `READ COMMITTED` transaction. Insert-or-detect the unique conflict, then lock/read the one row (`SELECT … FOR UPDATE`) and apply the transition rules atomically. Concurrent unknown→known updates serialize; same-known updates are idempotent; competing different-known updates yield one preserved value and a deterministic conflict. Avoid a global `SERIALIZABLE` transaction for this single-row state machine. Retry only expected transient database failures with the same call ID; never repeat the provider invocation as a telemetry retry.

## Recommended schema

**Entity:** `AIProviderCallTelemetry`
**Table:** `AIProviderCallTelemetries`
**Primary key:** `Id` (`uuid`), consistent with the repository's `AuditableEntity`.

| Field | Classification | Proposed storage / semantics |
|---|---|---|
| `Id` | `REQUIRED_NOW` | UUID surrogate primary key. |
| `CompanyId` | `REQUIRED_NOW` | Required UUID tenant owner and FK to `Companies.Id`; never derive it from a polymorphic outer ID. |
| `ProviderCallId` | `REQUIRED_NOW` | Required opaque string (current generator emits GUID-N); global unique constraint. Set before dispatch. |
| `OuterOperationType` | `REQUIRED_NOW` for known commercial calls; nullable for unclassified/internal calls | Nullable string discriminator, max 60. Existing values are `GuestMessage` and `CopilotOperation`. |
| `OuterOperationId` | `REQUIRED_NOW` for known commercial calls; nullable for unclassified/internal calls | Nullable UUID. Must be null exactly when the type is null. |
| `ProviderName` | `REQUIRED_NOW` when reliably known | Nullable string, max 80, to allow a `Pending` pre-call row and unknown adapter metadata. Do not store synthetic wrapper labels as vendor names. |
| `ModelName` | `REQUIRED_NOW` when reliably known | Nullable string, max 120. Current OpenAI value is configured/requested model, not provider-confirmed actual model. |
| `InputTokens` | `REQUIRED_NOW` | Nullable `bigint`; all three token fields null means usage unknown. |
| `OutputTokens` | `REQUIRED_NOW` | Nullable `bigint`; known zero is stored as `0`. |
| `TotalTokens` | `REQUIRED_NOW` | Nullable `bigint`; persist provider-reported total independently. |
| Separate `UsageKnown` / usage-state column | `NOT_REQUIRED` | Redundant with all-null versus all-set token tuple. Row existence and outcome distinguish no call from an observed call with unknown usage. Do not store aggregate `Partial`/`Complete` per call. |
| `Outcome` | `REQUIRED_NOW` | Required string, max 30. Minimum lifecycle values: `Pending`, `Success`, `Unavailable`, `Failed`. `Pending` means no terminal observation; cancellation can be represented as `Unavailable` plus category `Cancelled`. |
| `FailureCategory` | `USEFUL_LATER` (persist now when available) | Nullable allowlisted string, max 80. Store only safe categories, never raw exception/provider payloads. |
| `ProviderRequestId` | `USEFUL_LATER` | Nullable string, max 160; retain only actual provider response IDs when available. Never use as dedupe key. |
| `CreatedAt` | `REQUIRED_NOW` | Required inherited audit timestamp; create the `Pending` row immediately before dispatch and use it as the start/observation time. |
| `StartedAt` | `NOT_REQUIRED` as a separate column | Duplicates `CreatedAt` if the pre-call row is committed before dispatch. |
| `CompletedAt` | `REQUIRED_NOW` as nullable lifecycle field | Null while `Pending`; set when the provider boundary reaches a terminal result. |
| `UpdatedAt` | `REQUIRED_NOW` | Required inherited audit timestamp; updated on terminal result or enrichment. |
| `DurationMs` | `USEFUL_LATER` | Nullable bigint. Existing result metadata can provide it, but orchestration cancellation/exception wrappers may leave a default zero; store only a measured duration. |
| `RawException`, HTTP body, prompt/response, provider payload | `NOT_REQUIRED` | Sensitive or unnecessary for token telemetry. |
| Price, cost, currency, pricing version | `NOT_REQUIRED` in Phase 2B.3 | Explicitly deferred to Phase 2C. |

**Checks and indexes**

- `PK_AIProviderCallTelemetries (Id)`.
- Unique `UX_AIProviderCallTelemetries_ProviderCallId (ProviderCallId)`.
- FK `CompanyId → Companies.Id`.
- Check that outer type and ID are either both null or both non-null.
- Check that all token values are either all null or all non-null.
- Check that populated token values are non-negative. Do not require `TotalTokens == InputTokens + OutputTokens`; current source does not enforce that invariant.
- Check allowed lifecycle outcomes and require `CompletedAt IS NULL` for `Pending`, non-null for terminal outcomes.
- Index `(CompanyId, CreatedAt)` for tenant/billing-period scans.
- Index `(CompanyId, OuterOperationType, OuterOperationId)` for operation correlation, filtered to non-null outer IDs where supported.
- Provider/model and failure-specific indexes should be added only if measured analytics/query load requires them; they are not needed for the initial state machine.

## Tenant identity, outer references, and deletion

- Persist `CompanyId` directly. Outer IDs are not globally tenant-authoritative; tenant filtering, analytics, FK possibilities, and security require a direct tenant key. Resolve it from trusted tenant context and validate any parent correlation at the recorder boundary.
- Add a required Company FK. Recommended initial delete behavior is `RESTRICT`: token telemetry is a metadata-only usage/audit record, and tenant deletion should not silently erase reconciliation evidence. Tenant offboarding must define a retention period followed by explicit purge or anonymization. This policy must be reconciled with any legal erasure requirement before implementation; current source does not establish a tenant-erasure policy.
- Store `(OuterOperationType, OuterOperationId)` as a correlation pair, not a polymorphic FK. A discriminator plus UUID cannot be a normal FK; two nullable FKs would couple the table to today's message and Copilot entities; a new generic outer-operation table is unnecessary scope for 2B.3.
- Do not FK telemetry to `ConversationMessage` or `CopilotOperation`. Telemetry should survive parent deletion/soft deletion and support retained analytics. The scalar correlation remains useful even if the parent is removed.
- If a future requirement mandates relational referential integrity for outer rows, revisit a typed parent-link design separately. At recording time validate the parent belongs to `CompanyId` and expected type; consider soft-delete query filters when validating historical messages.
- Current `ConversationMessage` soft-deletes and cascades on physical conversation deletion. `CopilotOperation` has restrictive Company, Conversation, and ActorUser FKs. The telemetry schema should not introduce a new cascade path from either outer entity.

## Authoritative write boundary and transaction/failure policy

**Recommended boundary:** a provider-neutral `IAIProvider` recording decorator (or equivalent single call-boundary component) around the selected provider, with one `IAIProviderCallTelemetryRecorder`. It must receive explicit trusted `CompanyId`, the existing `ProviderCallId`, and optional outer identity; avoid EF/DbContext dependencies in provider SDK adapters. `AIProviderRequest` currently has no `CompanyId`, so pass an explicit call scope or trusted tenant value to the recorder rather than attempting to infer tenant from an outer ID. Do not write from both the adapter and endpoint/service layers.

1. Immediately before dispatch, commit a `Pending` row in a short telemetry-only transaction.
2. Invoke the provider outside any database transaction.
3. For success, provider failure, timeout, or cancellation, update that same row with terminal outcome and all reliably observed metadata/usage in a separate short transaction.
4. Do not enlist telemetry in the guest-message, Copilot-operation, or response persistence transaction. A later outer-operation failure must not roll back a successful provider-call observation.
5. Provider failure with a returned usage tuple persists both the failure outcome and known usage. Provider failure without usage persists null token values, not zeros.
6. A successful provider result remains a successful provider outcome even if later validation, fallback, message persistence, or Copilot-operation completion fails.

**Recommended failure policy:** the start record is required before dispatch. If the `Pending` insert still fails after bounded database-only retries, do not dispatch a potentially billable external provider call; surface the existing safe unavailable/fallback behavior and report the storage error. This prevents a new paid call with no durable call identity. If terminal update fails after the provider returned, retry only the telemetry update with the same ID, emit explicit error/alert telemetry, keep the durable row `Pending`/unknown, and do not invoke the provider again or discard an otherwise valid provider result merely because the DB write failed. This leaves a visible incomplete observation rather than a fabricated success/zero or an invisible call.

There is no generic telemetry outbox in the repository. A persistent outbox/spool is a separate reliability scope and cannot store the result while the same database is unavailable. Do not add an outbox in 2B.3 unless product requirements demand recovery of terminal result metadata across DB outages. The bounded retry must be explicit; no broad catch or success-shaped fallback.

## Aggregation source of truth

- Provider-call rows are the sole durable source of truth.
- Do not persist a second outer-operation aggregate in 2B.3. Its totals/completeness are derivable and a maintained copy can drift under replay, enrichment, conflicts, and concurrent writes.
- Keep the Phase 2B.2 in-memory aggregation only as request/result behavior if still needed; it is not an additional durable source.
- Derive operation aggregates by `(CompanyId, OuterOperationType, OuterOperationId)`. Zero rows means no observed calls; rows with null token tuples mean observed calls with unknown usage; all-set rows can be checked and summed; mixed rows are partial. Use checked summation and surface overflow.
- Future materialized/read models can be added if query measurements justify them, with provider-call rows remaining canonical.

## Existing billing/quota architecture

- `UsageRecord` is a period/metric accumulated quantity with unique tenant/metric/period semantics. It is read as quota usage, not a provider-call ledger.
- `UsageOperation` is a quota-consumption/idempotency event keyed by tenant, metric, period, and idempotency key.
- Do **not** write token telemetry to either table, do not create `UsageOperation` records for provider calls, and do not change `AiRequests` behavior.
- `UsageMetric.AiTokens` and `Quota.AiTokens` entitlements already exist, but the current provider paths consume `AiRequests`; there is no current `AiTokens` consumption. `SubscriptionEntitlementService` builds quota snapshots from `Quota.*` entitlements and `UsageRecords`, so an existing AiTokens entitlement may be exposed in a snapshot with its current `UsageRecord`-derived value (often zero). Phase 2B.3 should not alter this snapshot, add UI, enforce a token quota, or treat telemetry as quota usage.
- The repository's existing pre-deployment matrix must remain unchanged unless implementation is later approved and actually changes the migration set.

## Phase 2C compatibility, privacy, and query patterns

Capture now because it cannot reliably be reconstructed later:

- StayFlow ProviderCallId and provider-call start/terminal times.
- Tenant and optional outer-operation correlation.
- Provider name, requested/configured model, reported input/output/total token counts, and actual provider request ID when available.
- Outcome and coarse safe failure category.

Phase 2C may later join provider/model/timestamps to effective-dated pricing rules and calculate input/output cost, currency, and historical estimates. Do not store prices, costs, currency, or pricing versions in 2B.3. The current OpenAI adapter does not establish provider-resolved model identity; improve that adapter metadata before claiming exact historical model pricing.

Telemetry should contain no prompt, response, guest message content, raw provider payload, or exception body. `CompanyId`, call IDs, operation IDs, provider/model labels, token counts, outcome, and timestamps are sufficient for the proposed purpose.

Demonstrated query shapes and initial indexes:

1. Tenant plus time/billing period: `(CompanyId, CreatedAt)`.
2. Tenant plus outer operation: `(CompanyId, OuterOperationType, OuterOperationId)`.
3. Provider-call dedupe/lookup: global unique `ProviderCallId`.
4. Provider/model analytics and failure analysis: initially filter over tenant/time index; add compound indexes only after actual query plans/load justify the write/storage cost.
5. Operation totals: derive from provider-call rows; no aggregate table/index beyond outer correlation.

## Future implementation test plan

1. Known positive usage persists exactly.
2. Known zero persists distinctly from unknown.
3. Unknown usage persists as an observed row with all token fields null.
4. Provider failure before usage persists failure and unknown usage.
5. Provider failure/result with known usage retains both failure and usage.
6. Provider succeeds but outer operation later fails; telemetry remains.
7. Unknown→known enriches the same row.
8. Unknown replay is idempotent; known-same replay is idempotent; known→unknown does not downgrade.
9. Conflicting known usage is rejected without mutation.
10. Two distinct ProviderCallIds for one outer operation persist independently.
11. Tenant isolation and immutable CompanyId/outer identity.
12. Guest outer-message correlation and Copilot-operation correlation.
13. Provider/model/request-ID metadata persists only when supplied and accurately labeled.
14. Telemetry pre-call and terminal-write failure policy, explicit logging/alert, and no second provider invocation on write retry.
15. Concurrent observations for one ProviderCallId.
16. Derived no-call/all-unknown/partial/complete aggregates, known-zero semantics, and checked overflow.
17. Guest external-message replay returns early; no-external-ID retry creates a distinct message/call when it invokes again.
18. Copilot replay may invoke a second time under the same outer operation but has a new call ID and separate telemetry row.
19. Phase 2A `AiRequests` admission/replay/exhaustion/failure semantics are unchanged; no `AiTokens` quota writes or rejection.
20. Development/grounded/dormant/legacy/diagnostic paths follow the approved classification and do not fabricate commercial identities.

## Later PostgreSQL validation plan (design only; not executed)

Use an isolated disposable PostgreSQL database and separate contexts/connections; never use persistent `DefaultConnection`.

- Race two inserts for one ProviderCallId; assert one durable row.
- Race unknown insertion with known enrichment; assert one row and final known tuple.
- Race identical known observations; assert idempotent success.
- Race conflicting known values; assert deterministic conflict and no silent overwrite.
- Present one call ID under another CompanyId; assert no cross-tenant read/update and invariant conflict.
- Persist multiple distinct IDs for one outer operation and verify derived aggregate/completeness.
- Verify check constraints for all-null/all-set tokens, non-negativity, paired outer fields, and outcome/timestamp state.
- Retry telemetry persistence with the same call ID; assert provider invocation count does not increase.
- Test tenant and outer operation queries under actual PostgreSQL indexes.

## Migration and scope

| Item | Audit result |
|---|---|
| Likely future migration | Add one provider-call telemetry table, Company FK, unique call-ID constraint, demonstrated indexes, and state/token checks |
| Migration generated now | No |
| Migration applied now | No |
| Source/test/schema changed | No |
| Database contacted | No |
| OpenAI/external provider contacted | No |
| AiTokens enforcement | No |
| Pricing/cost/currency | No |
| Frontend/billing snapshot changes | No |
| Prompt/response logging | No |
| Reports staged/committed/pushed | No |

**Audit conclusion:** Phase 2B.2 is committed and current source supports moving to a persistence-design approval. Recommended design is a single provider-call ledger with immutable globally unique StayFlow call IDs, tenant-scoped access, optional typed outer correlation, nullable token triples, explicit call lifecycle/outcome, pre-call durable `Pending` insert, and operation aggregates derived from rows. Before implementation, approve the recommended telemetry write-failure policy and reconcile tenant-retention behavior with legal deletion requirements.
