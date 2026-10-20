# Sprint 20 — AI Token Telemetry
## Phase 2B.3 Persistence Architecture Audit

**Audit date:** 2026-10-19
**Repository:** `/workspace`
**Scope:** Read-only architecture review. No Phase 2B.3 implementation or migration was created.

## Phase 2B.2 closure

- Starting branch: `main`
- Starting HEAD: `c9becc02083a1b728dbe4f475aad61aa1d8ff0f1`
- Upstream: `origin/main`, same SHA
- Phase 2B.2 commits: `f4de255` and `c9becc0` (`Add runtime AI token aggregation`)
- Pushed: yes; HEAD equals upstream.
- Tracked/un-staged source changes: none.
- Staged files: none.
- Known pre-existing untracked reports remained untracked; this report is also untracked.

### Fresh validation

| Check | Result |
|---|---|
| `dotnet build backend/backend.csproj --no-restore` | Passed; 0 warnings, 0 errors |
| Focused `AITokenAggregationRuntimeTests`, `AIReplyOrchestratorGroundingTests`, `OpenAIAIProviderTests` | Passed; 50/50, 0 skipped |
| Targeted `CopilotCommercialBoundaryIntegrationTests`, `CopilotOperationAdmissionTests`, `ChatServiceTests`, `HostCopilotControllerIntegrationTests`, `PlatformAdmin` | Passed; 65/65, 0 skipped |
| Full `StayFlow.Api.Tests` suite | Passed; 1,126/1,126, 0 skipped |
| Frontend `npm test` | Passed; 43 files, 262 tests |
| Frontend `npm run typecheck` | Passed |
| Frontend `npm run build` | Passed; existing Rollup annotation and bundle-size warnings only |
| `dotnet ef migrations has-pending-model-changes` | Passed; no model changes since last migration |
| `git diff --check` | Passed |

Backend tests used a disposable PostgreSQL 16 cluster bound only to `127.0.0.1:55445`; the test connection variables were explicitly pointed at it. The pre-existing `ConnectionStrings__DefaultConnection` value was not used or displayed. The EF model check was also given only the disposable connection configuration and did not apply migrations. The temporary cluster is not a persistent application database.

## Current model

### Relevant entities and ownership

- `Company` is the tenant root. Its ID is the authoritative company identifier for tenant-owned data.
- `Conversation` has `CompanyId`.
- `ConversationMessage` has both `CompanyId` and `ConversationId`. It is soft-deletable and has query filters; a telemetry lookup by an old message ID may need `IgnoreQueryFilters()` while still requiring an exact company match.
- `CopilotOperation` has `CompanyId`, `ConversationId`, `ActorUserId`, lifecycle `Status`, and `CompletedAt`. Its `Id` is the durable outer-operation ID for Suggestions, Generated reply, and Workspace draft.
- `ApplicationDbContext` discovers entity configurations, enforces tenant ownership on writes when an authenticated tenant context exists, validates tenant foreign-key ownership, and sets `CreatedAt`/`UpdatedAt` for `AuditableEntity` records.

### Existing storage suitability

- **Dedicated provider-call telemetry storage:** none exists.
- **`UsageRecord`: unsuitable.** It stores a quota/billing-style accumulated quantity by `CompanyId`, metric, and period. It has a unique `(CompanyId, Metric, PeriodStartUtc)` index and is read as the accumulated amount used.
- **`UsageOperation`: unsuitable.** It stores a quota consumption/idempotency event keyed by company, metric, subscription period, and idempotency key. Reusing it for token observations would conflate telemetry with quota authority and could alter `AiRequests` semantics.
- **`ConversationMessage`: unsuitable as the ledger.** It contains message-scoped provider name/model/request-ID and outcome fields, but not token counts or one-row-per-provider-call identity; it does not represent Copilot or multiple attempts reliably.

AI orchestration results and `AiTokenUsageAggregation` are runtime-only. The current aggregation has the correct replay/enrichment semantics but disappears with its request/process and does not provide a durable provider-call history.

## Proposed entity

**Entity:** `AIProviderCallTelemetry`
**Table:** `AIProviderCallTelemetries`
**Purpose:** One row per StayFlow-observable logical provider invocation, not one row per outer customer operation and not one row per hidden provider SDK/network retry.

| Field | Proposed representation | Source and purpose |
|---|---|---|
| `Id` | `uuid`, PK | Surrogate row identity; consistent with `AuditableEntity`. |
| `CompanyId` | `uuid`, required, FK to `Companies.Id` | Tenant owner, authoritatively resolved/validated before recording. |
| `OuterOperationType` | nullable string, max 60 | Existing enum value (`GuestMessage` or `CopilotOperation`) where a durable commercial outer operation is supplied. Null means deliberately unclassified, not billable. |
| `OuterOperationId` | nullable `uuid` | Existing message or Copilot operation ID. Must be null exactly when `OuterOperationType` is null. |
| `ProviderCallId` | string, max 160, required | `AIProviderRequest.ProviderCallId`, created by StayFlow before `IAIProvider.GenerateAsync`. Current factory emits Guid-N text, but the wrapper constructor accepts any non-empty string; do not map it to PostgreSQL `uuid` unless that contract is first tightened. |
| `ProviderName` | nullable string, max 80 | `AIProviderResult.ProviderName` when it names the actual provider. Do not persist synthetic orchestration labels such as `TimedOut` as a provider. |
| `ModelName` | nullable string, max 120 | `AIProviderResult.ModelName` when actually supplied by the adapter/configuration. |
| `InputTokens`, `OutputTokens`, `TotalTokens` | nullable `bigint` | Exact provider usage tuple. All null means unknown; all non-null means known, including known `0/0/0`. |
| `Outcome` | required string, max 30 | Proposed call lifecycle: `Pending`, `Success`, `Unavailable`, or `Failed`. `Pending` represents a started call without a durable terminal observation. It is distinct from the outer operation outcome. |
| `FailureCategory` | nullable string, max 80 | Provider/orchestrator category when available (`Timeout`, `Network`, `Cancelled`, etc.); absent when not known. |
| `ProviderRequestId` | nullable string, max 160 | Actual external provider response ID when returned. `OpenAIResponsesClient` maps `ResponseResult.Id`; exceptions may provide none. The Development provider's generated ID is synthetic and should not be represented as a vendor request ID. |
| `DurationMs` | nullable `bigint` | Measured call duration when reliable. The current result field exists, but outer timeout/exception wrappers can leave its default value at zero; do not interpret such a default as a measured duration. |
| `CreatedAt`, `UpdatedAt` | required `timestamptz` | Inherited audit fields. Create the row immediately before invocation; `CreatedAt` is the durable start/observation time. `UpdatedAt` changes on enrichment/completion. |
| `CompletedAt` | nullable `timestamptz` | Set when the provider boundary reaches a terminal result; null for a call left `Pending`, e.g. process termination before completion is recorded. |

No `UsageKnown` boolean or aggregate-completeness column is needed: the all-null/all-non-null token tuple is the source of truth. No separate `ObservedAt` or `StartedAt` is needed if the initial row is created immediately before the provider call. Do not persist prompt/response text or other content as part of token telemetry.

## Schema and tenant isolation

### Keys, constraints, and indexes

- Primary key: `PK_AIProviderCallTelemetries (Id)`.
- Unique index: `UX_AIProviderCallTelemetries_CompanyId_ProviderCallId` on `(CompanyId, ProviderCallId)`.
  - Do not use `ProviderCallId` alone as the database key. Tenant-scoped uniqueness ensures a collision in another tenant can neither alias nor mutate that tenant's row.
  - ProviderCallIds are generated as GUID-N strings by the current factory, but the value type does not enforce GUID syntax. A global UUID constraint is therefore not an accurate encoding of the current type contract.
- Tenant/time index: `(CompanyId, CreatedAt)`.
- Outer-operation index: `(CompanyId, OuterOperationType, OuterOperationId)`, filtered to `OuterOperationId IS NOT NULL`.
- Provider/model analytics index: `(CompanyId, ProviderName, ModelName, CreatedAt)`. Avoid separate indexes for each nullable provider/model column.
- FK: `CompanyId -> Companies.Id`, with tenant data deletion behavior aligned with the existing usage entities (cascade). No FK to outer-operation rows.
- Checks:
  - Token tuple is either all null or all non-null.
  - Each non-null token count is non-negative.
  - Outer-operation type and ID are both null or both non-null.
  - `DurationMs` is null or non-negative.
  - `Outcome` is restricted to the supported lifecycle values.
- Do **not** add `TotalTokens = InputTokens + OutputTokens`: the current contract stores the provider's reported total separately, and a cross-provider equality guarantee is not established.

### Outer-operation relationship

Store `OuterOperationType` and `OuterOperationId` as a correlation pair, not a polymorphic FK. The supported parents are `ConversationMessage` and `CopilotOperation`; one FK cannot safely target both. Adding two nullable FK columns plus discriminator/checks would duplicate the current contract and add unnecessary schema coupling. The recorder must resolve the typed parent and verify its company before insertion; read/update queries must always include `CompanyId` as well as `ProviderCallId`.

For unclassified calls, both outer fields remain null. This is not an invented `GuestMessage` or `CopilotOperation`. A cross-tenant ID mismatch must be rejected before insert/update without returning the other tenant's row or details.

### Tenant derivation

For a commercial record, resolve the authoritative owner from the durable parent:

- `GuestMessage`: `ConversationMessage.CompanyId`, validating the message/conversation relationship.
- `CopilotOperation`: `CopilotOperation.CompanyId`.

Compare that owner with the already-validated orchestration tenant. Do not trust an arbitrary caller-supplied `CompanyId` or accept a parent from a different company. For deliberately unclassified calls, derive company from the persisted conversation/context being orchestrated; do not construct a commercial outer identity. The FK and composite unique index provide additional boundaries but do not replace scoped queries or parent validation.

## Durable idempotency and concurrency

The recorder should receive the same `ProviderCallId` from pre-invocation through completion and use it on every persistence retry. Recommended row-state rules:

| Observation | Durable behavior |
|---|---|
| New ID, unknown usage | Insert a `Pending` row with null tokens before invocation; if only a terminal observation is available, insert the terminal row with null tokens. |
| Same ID, unknown replay | No-op; never create another row. |
| Unknown to known | Enrich the same row with all three token values. |
| Same known tuple | Idempotent no-op. |
| Known to unknown | No-op; never erase known usage. |
| Different known tuple | Deterministic conflict; preserve the prior tuple and metadata. |
| Different ProviderCallId | Independent row, even for the same outer operation. |
| Provider failure without usage | Preserve the call row with null tokens and its available outcome/category. |

Identity/tenant/outer-operation fields are immutable for a ProviderCallId. Fill nullable provider/model/request metadata only when reliable; equal replay is a no-op and conflicting non-null identity metadata should be rejected rather than overwritten. `Pending` can transition to a terminal outcome. A terminal replay with the same outcome/usage is idempotent; a contradictory terminal result is a conflict.

**PostgreSQL concurrency recommendation:** use an explicit short transaction at `READ COMMITTED`; attempt `INSERT ... ON CONFLICT (CompanyId, ProviderCallId) DO NOTHING`, then read that row by both key fields `FOR UPDATE`, evaluate the transition, and update only after all checks pass. PostgreSQL's unique index serializes concurrent inserts of the same tenant/key; the row lock serializes enrichment and conflict decisions. `SERIALIZABLE` is not required for this single-key transition.

Treat only the expected composite unique constraint as replay. Do not convert arbitrary unique violations into successful idempotency. Bounded retries for `40001` serialization failures and `40P01` deadlocks may retry the database transition with the **same** ProviderCallId; never repeat the provider call because telemetry persistence raced. Keep the transaction limited to telemetry DB work. Existing quota retry code uses bounded retries and recognizes these PostgreSQL states, but telemetry should not depend on quota semantics.

## Provider metadata, outcome, and completeness

- Provider and model are currently surfaced on `AIProviderResult`; OpenAI's model is configured in `OpenAIOptions` and returned by the adapter, while the Development provider has deterministic metadata.
- The OpenAI response ID and usage are available only when the SDK returns a response. On transport/auth/provider exceptions, request ID and usage may be absent. `AIProviderResult` must not turn missing values into fabricated metadata.
- `AIProviderOutcome` currently has `Success`, `Unavailable`, and `Failed`. The wrapper catches timeouts, network errors, cancellation, and unexpected exceptions; outer orchestration catches can produce synthetic provider labels and may not retain provider/model/request ID. Store actual known metadata as nullable, not the synthetic label as a vendor.
- A provider-call outcome means only the provider boundary's result. `AIReplyOrchestrator` may later reject content, choose safe fallback, or mark the outer operation failed. Do not conflate these outer outcomes with provider success/failure. `FallbackUsed` is an outer orchestration result, not a provider-call outcome.
- Per-call usage is known (three non-null values) or unknown (three null values). Aggregate `Complete`, `Partial`, and `Unknown` should be derived over persisted provider-call rows for the requested outer operation/time scope, with the same zero-call versus all-unknown distinction as Phase 2B.2. `Complete` only means every included observable row has usage; it does not claim global provider-cost coverage.
- Aggregate sums may overflow the existing `long` contract even if each row is valid. Analytics must preserve checked/explicit overflow behavior rather than silently wrapping.

## Write boundary and telemetry-write failure policy

**Recommended boundary:** a provider-neutral `IAIProviderCallTelemetryRecorder` owned by the orchestration/provider-call boundary, invoked for each `IAIProvider.GenerateAsync` call (not by OpenAI EF-adapter code and not only from a customer endpoint). It should:

1. Use the StayFlow-created ID and validated tenant/outer context.
2. Persist a `Pending` row immediately before invoking the provider.
3. Complete/enrich that same row after a result, timeout, failure, or cancellation, preserving unknown tokens when usage is unavailable.
4. Log and meter recorder failures explicitly, with bounded database-only retries.

This per-call boundary is necessary because an outer-operation snapshot loses per-call metadata and the current aggregation is only created when outer identity is present. It also allows future unclassified provider calls to be recorded without inventing a commercial ID.

**Unresolved policy decision — do not implement until approved:** whether a telemetry database failure should block an external provider call.

- **Recommended default:** fail open for this telemetry-only, non-billing feature. Keep the AI operation/customer response available; log/alert the ProviderCallId and telemetry write error, retry only the persistence operation in a bounded way, and report a coverage gap. If the pre-call insert fails, proceed only under this explicit policy; the call may then have no durable row.
- **Trade-off:** fail open cannot promise durable coverage during a database outage or a process crash between provider response and completion write. A pre-call `Pending` row narrows but does not eliminate that gap.
- **Alternative:** fail closed before invoking the provider if the `Pending` row cannot be written. This improves accounting completeness but makes AI availability depend on the telemetry database.

The repository has no existing token-telemetry reliability policy that resolves this choice. Product/operations must approve one. Do not silently make telemetry persistence failure a customer-visible failure or silently swallow it.

## Replay behavior and limitation

`ProviderCallId` is created immediately before `IAIProvider.GenerateAsync`. A replay of an outer operation that starts a **new provider invocation** is a new call and must get a new ProviderCallId and row. This is important: Copilot admission recognizes a replay but still proceeds through orchestration; the stable `CopilotOperation.Id` does not mean the provider was previously invoked or that the invocation result should be reused. The guest path may similarly reuse a stored message identity while executing orchestration again. `AiRequests` idempotency remains separate and unchanged.

If a completed call's telemetry write is retried, reuse its original ProviderCallId. A duplicate of that ID is a replay of the same call; a new ID represents a new invocation. The durable pre-call row carries the identity across request/process lifetimes. If a process dies after the provider ran but before its result/usage is persisted, the durable row remains `Pending`/unknown; a later, genuinely new invocation gets a new ID and does not overwrite it.

This design guarantees telemetry deduplication for a **known ProviderCallId**, not exactly-once remote provider execution. The current code does not durably record/replay provider responses or provide provider-side idempotency. It cannot infer from a pending outer operation whether a remote call completed before a crash; it must not reuse the old ID for a new remote call merely to suppress another telemetry row.

## Current call-path coverage

| Path | Current source identity/provider behavior | Phase 2B.3 recommendation |
|---|---|---|
| Guest reply | `ChatService` passes `ConversationMessage.Id` as `GuestMessage`; quota key remains `ai-request:guest-message:{id:N}`. | Persist each actual invocation with that outer pair; repeated invocation of the same message is a distinct provider call. |
| Suggestions | `CopilotService` passes durable `CopilotOperation.Id` as `CopilotOperation`. | Persist each invocation, including replay invocations, under the same outer ID but unique call IDs. |
| Generated reply | `CopilotService` passes durable `CopilotOperation.Id`. | Same as Suggestions. |
| Workspace draft | `HostCopilotWorkspaceService` passes durable `CopilotOperation.Id`. | Same as other Copilot invocations. |
| Copilot retrieval diagnostics | Current orchestrator call has no commercial outer ID and does not create the Phase 2B.2 aggregate; this pre-existing path must not be assigned a fabricated Copilot operation or quota key. | If included in provider-call telemetry, record with authoritative company and null outer pair; do not change AiRequests admission in this phase. |
| Grounded concierge | `GroundedConciergeResponseGenerator` uses a separate language-model abstraction. Both branches of `ConciergeLanguageModelProviderFactory` currently return `DevelopmentConciergeLanguageModel`; its result has no token usage or ProviderCallId. | No current paid-provider coverage gap. Before enabling an external/paid model, add call identity, usage, and recorder integration to that boundary or explicitly document the gap; do not claim current coverage for it. |
| `ConversationAIExchangeService` | Registered but currently unused; it adds a guest message and calls the orchestrator without outer identity or AiRequests admission. | Preserve `REGISTERED_BUT_CURRENTLY_UNUSED` / `DORMANT_UNCLASSIFIED`. If actually invoked and telemetry is desired, record only unclassified telemetry with company derived from the conversation; do not classify it as billable or fabricate `GuestMessage`. |
| Legacy `AIOrchestrator` | The controller returns HTTP 410; its legacy direct provider code remains separate. | Do not claim active commercial coverage. If that endpoint/path is reactivated, wire and test its call boundary before claiming full provider coverage. |

`AIReplyOrchestrator` currently aggregates only if both outer identity fields are present and observes token usage in memory; a durable recorder must observe individual calls independently of aggregate creation.

## Migration and deployment

- Add one additive migration after the existing `20260922185101_AddCopilotOperations` migration; do not edit or regenerate that historical migration.
- The proposed telemetry table references `Companies` only and stores outer-operation IDs as correlations, so it has no schema-level dependency on `CopilotOperations`. EF migration history still applies pending migrations in order; if `AddCopilotOperations` is unapplied, it must be applied before the subsequent migration in the same migration sequence/release.
- No existing telemetry rows require backfill. The new table begins empty.
- Required tenant/key/status/token fields can be non-null on a new empty table; provider/model/request ID, outer-operation pair, usage triple, duration, and completion time are nullable as described.
- The migration is additive. Apply schema before deploying code that writes to it; the application currently has no startup `Migrate`/`MigrateAsync` call found in source.
- No migration was generated or applied in this audit.

## Phase 2B.3 test plan

### Unit tests

- Insert known and unknown observations; preserve known zero versus unknown.
- Same unknown replay, unknown-to-known enrichment, same-known replay, known-to-unknown no-downgrade, conflicting-known rejection without mutation.
- Distinct ProviderCallIds under one outer operation remain distinct.
- Immutable tenant/outer identity and metadata-conflict behavior.
- Provider failure, timeout, and cancellation with unknown usage retain the observed call and correct call outcome/category.
- Telemetry write failure behavior follows the approved fail-open/fail-closed policy and reports failures explicitly.
- Verify aggregate completeness derives from rows, including zero observed versus all unknown, and overflow is explicit.

### EF/integration tests

- New scope/context reads persisted telemetry.
- Validate Company FK, token nullability/non-negative checks, outer-pair check, status constraint, and index shape.
- Tenant-scoped insert/read/enrichment/conflict behavior, including a ProviderCallId reused in another company.
- Typed outer correlation for GuestMessage and CopilotOperation; unclassified rows have no outer identity.
- Verify recorder is called for every provider invocation, including exception/timeout paths, and does not invoke the provider a second time during persistence retries.
- Exercise Guest, Suggestions, Generated reply, Workspace, diagnostics/unclassified, grounded-path gate, and dormant ConversationAIExchange behavior.

### PostgreSQL-specific concurrency tests

Use separate contexts/connections and a real disposable PostgreSQL database; EF InMemory cannot prove uniqueness, row locking, or transaction behavior:

- Concurrent same-ID insert yields one tenant row.
- Concurrent unknown/known observations yield one row and known final usage.
- Concurrent same-known observations are idempotent.
- Concurrent conflicting known observations produce a deterministic conflict and preserve the winner's tuple.
- Same ProviderCallId in another tenant cannot read or mutate the first tenant's record.
- Retry simulated serialization/deadlock failures at the telemetry transaction only, reusing the same ProviderCallId.

## Phase 2C exclusions

Phase 2B.3 stores telemetry facts only. Exclude pricing tables, cost calculation, currency, pricing versions/effective dates, invoices, billing, and `AiTokens` quota enforcement. Do not change `AiRequests` admission, quota keys, replay charging, or provider-failure charging.

## Audit result

**Blocker:** owner approval is required for the telemetry-write outage policy (recommended: fail open with explicit alerting and a declared coverage gap; alternative: fail closed before provider invocation). This decision affects whether an external provider call may proceed when a durable pre-call record cannot be written.

The rest of the persistence design is implementable with the recommendations above. In particular, the durable uniqueness boundary, tenant scoping, per-call identity, and new-invocation-versus-persistence-replay distinction are specified. No Phase 2B.3 source, configuration, test, or migration file was changed or created; only this requested untracked audit report was written.

## 2026-10-20 current-source reconciliation addendum

This addendum rechecked the current source and repository state against the
Phase 2B.3 read-only architecture-audit request. Source at
`c9becc02083a1b728dbe4f475aad61aa1d8ff0f1` is authoritative where earlier
audit reports disagree.

### Verified repository state and Phase 2B.2 provenance

- Branch `main`; HEAD and `origin/main` are both
  `c9becc02083a1b728dbe4f475aad61aa1d8ff0f1`.
- No tracked modifications and no staged changes existed at audit start.
- The seven previously listed `SPRINT20_*.md` reports were untracked.
- `f4de25571f3405e98cebde84a5f88d91b08830da` is a direct child of
  `9d8bbee93cfff0818654aaf800aa8711a2dd394a`. It introduced runtime call
  identity, outer identity propagation, the in-memory aggregation type, and
  initial tests.
- `c9becc02083a1b728dbe4f475aad61aa1d8ff0f1` is a direct child of `f4de255`.
  It adds orchestration snapshots/observation across provider failure and
  fallback paths, provider identity propagation and tests, and integration
  fixture fixes. It is a follow-up completion, not a duplicate patch. Both
  commits are required for the current Phase 2B.2 behavior.
- Neither commit changes `SubscriptionEntitlementService`, migrations,
  frontend source, or the audit reports.

### Earlier reports: current versus stale

- `SPRINT20_AI_TOKEN_AGGREGATION_AUDIT.md` and
  `SPRINT20_AI_TOKEN_TELEMETRY_AUDIT.md` contain earlier-baseline sections
  that describe provider identity/aggregation as absent. Those statements
  predate the two Phase 2B.2 commits and are stale. Their later closure and
  current-baseline sections describe the implemented runtime model.
- `SPRINT20_AI_USAGE_METERING_AUDIT.md` was written against an older billing
  baseline. Its general conclusion that AiTokens is not quota-enforced is
  still supported by current source, but old quota-key and service-flow
  descriptions must not override the current keys and source paths.
- The prior version of this persistence report recommends `ON DELETE CASCADE`
  to match usage tables. That conflicts with its own requirement that
  telemetry not disappear unexpectedly when a tenant is deleted. This
  addendum supersedes that FK recommendation with `RESTRICT`; deletion/
  retention policy must be explicit.

### Current runtime contract

Verified in `backend/DTOs/AIProvider` and
`backend/Services/AI/Orchestration`:

| Type/property | Current type/nullability | Meaning |
|---|---|---|
| `AiTokenUsage.InputTokens`, `OutputTokens`, `TotalTokens` | `long`, non-null | Provider-reported usage tuple; total is separately reported, not derived. |
| `ProviderCallId.Value` | `string`, non-null | Value object rejects null/empty/whitespace; `New()` emits GUID-N text. The constructor does not require GUID format or cap length. |
| `OuterOperationType` | enum, values `GuestMessage`, `CopilotOperation` | Runtime commercial correlation discriminator. |
| `OuterOperationId` | `Guid` in snapshot/context; `Guid?` on request/result | Durable message/operation ID when classified; absent for dormant/unclassified paths. |
| `ProviderCallContext` | non-null outer type, `Guid`, `ProviderCallId` | Helper record; its outer fields are required and it is not used as the optional-request contract. |
| `AIProviderRequest.ProviderCallId` | `ProviderCallId`, non-null; initialized with `New()` | `AIReplyOrchestrator.InvokeProviderWithTimeoutAsync` explicitly creates a fresh ID immediately before `IAIProvider.GenerateAsync`. |
| `AIProviderRequest.OuterOperationType`, `OuterOperationId` | `OuterOperationType?`, `Guid?` | Both optional; current commercial callers set both. |
| `AIProviderResult.TokenUsage` | `AiTokenUsage?` | `null` means unknown; non-null `0/0/0` is known zero. |
| `AIProviderResult.Outcome` | `AIProviderOutcome` enum | Current values are `Success`, `Unavailable`, `Failed`; no pending lifecycle state exists in the runtime result. |
| `AIProviderResult.ProviderName` | `string` (defaults empty) | Can be synthetic in orchestration catch results; not always a reliable vendor name. |
| `AIProviderResult.ModelName`, `RequestId`, `FailureCategory` | `string?` | Optional actual metadata. |
| `AIProviderResult.DurationMs` | `long` | Defaults to zero; zero is not necessarily a measured duration on a synthetic catch result. |
| `AiTokenUsageAggregation.ProviderCallCount` | `int` | Distinguishes no calls from observed calls whose usage is all unknown. |
| `AiTokenUsageAggregation.Completeness` | `AiTokenUsageCompleteness` | `Unknown`, `Partial`, or `Complete`, derived from known/unknown call sets. |
| `AiTokenUsageSnapshot` token sums | `long`, non-null | Known sums only; the tuple is not a durable provider-call ledger. |

The runtime aggregator is created only when both optional outer identity
values exist. `Observe` records known or unknown usage in memory after the
provider returns or in caught exception/cancellation paths and emits a log.
The snapshot is returned in `AIReplyOrchestrationResult`; commercial service
response mappings do not persist it. After request completion, no durable
provider-call record remains. No Phase 2B.2 source includes a database entity,
repository, or write.

### Current paths and observability boundary

| Path | Admission and durable outer identity | Provider call / telemetry boundary |
|---|---|---|
| Guest Chat | `ChatService.SendGuestMessageAsync` ensures `AiConcierge`, then consumes `AiRequests` with `ai-request:guest-message:{guestMessage.Data.Id:N}`. The persisted `ConversationMessage.Id` is passed as `GuestMessage`. | `AIReplyOrchestrator` allocates a call ID before `IAIProvider.GenerateAsync`. OpenAI usage returns through `OpenAIResponsesClient.MapTokenUsage` -> `OpenAIAIProvider` -> `AIProviderResult` -> in-memory observation. |
| Copilot Suggestions | `CopilotService.GetSuggestedRepliesAsync` admits the durable `CopilotOperation.Id`; quota key is `ai-request:copilot-operation:{operationId:N}`. | Same orchestrator/provider boundary; outer type is `CopilotOperation`. |
| Copilot Generated Reply | `CopilotService.SuggestHostReplyAsync` admits the durable operation ID with the same canonical AiRequests key. | Same orchestrator/provider boundary. |
| Workspace Draft | `HostCopilotWorkspaceService.GenerateDraftAsync` admits `WorkspaceDraft` by its operation ID. LLM orchestration is conditional on `HostCopilot:EnableLlmWording`. | When enabled, passes `CopilotOperation` identity into the shared orchestrator; otherwise deterministic draft generation does not make a provider call. |

AiRequests admission remains separate from telemetry and unchanged. A repeated
outer operation may invoke the provider again; each actual new invocation gets
a new ProviderCallId. A persistence retry for one invocation must reuse that
call's original ID.

`ConversationAIExchangeService` is still registered in DI but no source
caller was found. Its code adds a guest message and calls
`AIReplyOrchestrator` without `AiRequests` admission and without an outer
identity. It remains `REGISTERED_BUT_CURRENTLY_UNUSED` /
`DORMANT_UNCLASSIFIED`; do not fabricate a `GuestMessage` identity.

### Grounded concierge path and coverage seam

`AIReplyOrchestrator` can call `GroundedConciergeResponseGenerator` before
the `IAIProvider` path. That generator uses the distinct
`IConciergeLanguageModelProviderFactory` and does not assign
`ProviderCallId`, outer identity, or token usage. In the current
`ConciergeLanguageModelProviderFactory`, both `Enabled` branches resolve
`DevelopmentConciergeLanguageModel`; it is deterministic and does not call an
external/paid LLM. Therefore this is not a current paid-provider usage gap.
If the factory is changed to return an external model, the new provider
boundary must be instrumented before claiming complete paid-call coverage.
Grounded invocations bypass the `IAIProvider` runtime aggregate today.

### Provider outcome interpretation

- A returned `AIProviderResult` gives one of `Success`, `Unavailable`, or
  `Failed`; usage can be null for any outcome, and some returned failure
  results can carry provider-reported usage (for example, an incomplete
  response).
- `OpenAIAIProvider` converts cancellation, timeout, network/provider
  exceptions into result outcomes/categories. If an `IAIProvider`
  implementation instead throws, the orchestrator observes unknown usage and
  returns synthetic `TimedOut` or `Exception` provider labels, except caller
  cancellation, which is observed and rethrown.
- A successful provider result can later be rejected by validation/safety or
  lead to deterministic fallback. That does not change the provider-call
  outcome. Fallback is an outer orchestration result, not a provider result.
- The application-level provider boundary exposes a result/exception, but
  does not prove whether a remote request was transmitted if an exception
  occurs. The SDK may have transport behavior below this boundary that this
  source does not separately instrument.
- Persist call outcome separately from usage-known state and from
  customer-visible Copilot/message operation outcome.

### Proposed minimal durable entity and field disposition

Proposed entity `AIProviderCallTelemetry`, table
`AIProviderCallTelemetries`, with a surrogate `Guid Id` primary key and one
row per StayFlow `IAIProvider.GenerateAsync` invocation attempt. Do not store
prompt or response content.

| Candidate field | Disposition | Recommendation |
|---|---|---|
| `Id` | REQUIRED | Surrogate UUID PK, consistent with `AuditableEntity`; not the replay key. |
| `ProviderCallId` | REQUIRED | Required string; unique with `CompanyId`. Use current StayFlow ID, never external request ID. A 160-character cap matches repository key conventions but must be validated at the boundary because the current value object accepts longer strings. |
| `CompanyId` | REQUIRED | Required tenant owner/FK. Resolve from trusted orchestration context and validate against any typed outer parent. Do not infer tenant for analytics by joining through an optional outer ID. |
| `OuterOperationType` + `OuterOperationId` | OPTIONAL pair | Nullable together. Store current enum value and Guid as a correlation pair; do not add polymorphic FK. Null is legitimate for unclassified internal paths. |
| `ProviderName` | OPTIONAL | Nullable; persist only a reliable provider identifier. Do not store synthetic `TimedOut`/`Exception` wrapper names as a vendor. |
| `ModelName` | OPTIONAL | Nullable; persist only if known from provider/configuration. Do not fabricate from an empty option. |
| `InputTokens`, `OutputTokens`, `TotalTokens` | OPTIONAL triple | Nullable `bigint`; all null = unknown, all set = known, including `0/0/0`. Enforce all-null/all-set and non-negative values. Store provider total as reported; do not impose `total = input + output`. |
| `Outcome` | REQUIRED | String/enum with `Pending`, `Success`, `Unavailable`, `Failed`. `Pending` is a durable unresolved attempt, not proof the remote service received it. Keep outcome independent from token tuple. |
| `FailureCategory` | OPTIONAL | Nullable string; store category when actually known, including cancellation/timeout/network classifications. |
| `ProviderRequestId` | OPTIONAL | Nullable string from actual provider response ID. OpenAI adapter maps `ResponseResult.Id` to `OpenAIProviderResponse.RequestId`; Development's generated ID is synthetic and should not be labeled an external provider request ID. Never use this for dedupe. |
| `CreatedAt`, `UpdatedAt` | REQUIRED | Inherited `AuditableEntity` timestamps. If row is inserted before dispatch, `CreatedAt` records the pre-call observation/start boundary; `UpdatedAt` is maintained on enrichment. |
| `CompletedAt` | OPTIONAL | Nullable; set on a terminal provider result. Null while `Pending`, distinguishing unresolved rows. `UpdatedAt` alone is not a semantic completion timestamp. |
| `DurationMs` | OPTIONAL | Nullable `bigint`; persist only if measured. Existing zero default on synthetic results must not be mistaken for measured zero duration. |
| `UsageKnown` / aggregate `Completeness` | REJECT | Redundant with all-null/all-non-null token tuple and derived row set. Derive `Complete`/`Partial`/`Unknown` at query time for a defined scope. |
| `StartedAt` separate from `CreatedAt` | DEFER | Not needed if row is inserted immediately before provider invocation; add only if lifecycle timestamps later need distinct definitions. |
| Cost, currency, price, billing-period fields | DEFER | Explicit Phase 2C scope; exclude now. |

### Identity, FK, indexes, and deletion

- Durable identity: surrogate `Id` plus unique `(CompanyId, ProviderCallId)`.
  The composite unique key deduplicates retries within a tenant and forces
  all reads/updates to scope both dimensions. It is not a global exactly-once
  guarantee and should not permit an unscoped lookup. Keep the ProviderCallId
  as opaque text; its factory currently emits GUID-N but its type contract is
  not GUID-only.
- `CompanyId` is directly stored and references `Companies.Id`. Recommend
  `DeleteBehavior.Restrict`, not the prior report's cascade: telemetry is
  operational evidence and should not disappear implicitly with tenant
  deletion. Define an explicit retention/anonymization purge policy before
  tenant deletion is needed. Existing `UsageRecord`/`UsageOperation` use
  cascade, but `ConversationMessage`, `CopilotOperation`, and other
  durable operational entities use Restrict; telemetry retention takes
  priority over imitating quota rows.
- Store `OuterOperationType` / `OuterOperationId` as a nullable polymorphic
  correlation pair, not FK columns. A single FK cannot target both
  `ConversationMessages` and `CopilotOperations`. Two nullable FKs would bind
  the telemetry table to today's commercial enum and complicate future
  internal operation types. Validate the typed parent and company before
  writing; always query using `CompanyId`.
- Required indexes: unique `(CompanyId, ProviderCallId)`; `(CompanyId,
  CreatedAt)` for tenant period queries; `(CompanyId, OuterOperationType,
  OuterOperationId)` for operation aggregation, preferably filtered to
  non-null outer ID. Defer provider/model composite index until measured query
  workload justifies its write/storage cost. Do not add an independent
  CompanyId index unless query plans require it.
- One additive migration for the new telemetry table only. No changes to
  `20260922185101_AddCopilotOperations.cs`, existing snapshots outside the
  generated additive migration, or quota schema.

### Durable replay, concurrency, and failure limits

Use one atomic database row transition per `(CompanyId, ProviderCallId)`:

| Input/state | Required behavior |
|---|---|
| First observation | Insert `Pending` with null token tuple immediately before invoking provider. |
| Pending -> terminal unknown | Preserve null usage and write terminal outcome/category/metadata. |
| Pending/unknown -> known | Enrich the same row once with all three usage values; complete outcome as observed. |
| Unknown replay | No-op except fill missing reliable metadata / terminal completion where the prior row is still pending. |
| Same known replay | Idempotent no-op when tuple/outcome agree. |
| Known -> unknown | Never clear token values. |
| Conflicting known tuple or immutable identity | Deterministic conflict; no partial mutation. |
| Different call ID | Independent row, including calls for the same outer operation. |

Recommended PostgreSQL implementation: a short `READ COMMITTED` transaction;
attempt parameterized `INSERT ... ON CONFLICT (CompanyId, ProviderCallId) DO
NOTHING`, then select the tenant-scoped row `FOR UPDATE`, validate transition
and immutable metadata, and update only after validation. The unique constraint
serializes first insert; row lock serializes enrichment/conflict decisions.
Recognize only the expected unique key as replay. Retry deadlock/serialization
failures on telemetry writes with the same ProviderCallId and never repeat
the provider call. Existing AiRequests admission uses Serializable
transactions and bounded retry for quota invariants; telemetry is a single
row state machine, so Serializable is not inherently necessary.

For failure durability, pre-insert `Pending` before the provider call and
finalize immediately after a returned result or in exception/cancellation
handling, before later orchestration validation/fallback. That preserves a
provider success even if later orchestration fails. A process crash after
insert but before actual network dispatch can leave a false-positive pending
attempt; a crash after dispatch but before completion can leave a pending row
for a call whose actual outcome is unknown. No database/provider distributed
transaction exists, so exactly-once remote execution or perfect observed-call
certainty cannot be promised. New provider invocation = new ID; persistence
retry = same ID.

**Policy decision still required before implementation:** whether telemetry
write failure is fail-open or fail-closed. Recommended operational default
for non-billing telemetry is fail-open with explicit logging/alerting and
documented coverage gaps; fail-closed prevents provider invocation when the
pre-call row is unavailable but makes AI availability depend on telemetry
storage. Neither behavior should be silently selected.

### Completeness, billing boundary, and analytics

- No provider rows for an outer operation: count `0`, completeness `Unknown`,
  aggregate total `null`.
- Rows exist and every token tuple is null (including a pending row): count
  greater than zero, completeness `Unknown`, total `null`.
- Known zero is `(0,0,0)`, not null; all-known rows derive `Complete`.
- Mixed known/unknown rows derive `Partial`.
- Store the individual rows and derive aggregate state. Scope by
  `(CompanyId, OuterOperationType, OuterOperationId)` or by tenant/time;
  `Complete` is only complete for persisted/observed rows, not proof no
  provider path was omitted.
- Future-supported queries: tokens by tenant/time, provider/model, outer
  operation; observed unknown usage rate; provider failure/unavailability
  rate. No dashboard is in 2B.3.
- No AiTokens quota admission, plan-limit rejection, `UsageRecord`/
  `UsageOperation` AiTokens writes, pricing, cost, invoice, or currency.
  AiRequests remains the only current commercial quota authority.

### Required implementation test plan

Unit/state-machine tests:

1. first known and first unknown persistence;
2. unknown -> known enrichment;
3. unknown replay, known-same replay, known -> unknown no-downgrade;
4. known conflict and no-mutation after conflict;
5. distinct IDs on one outer operation;
6. known zero persisted distinctly from all-null unknown;
7. no calls versus one/all-unknown calls and derived completeness;
8. provider success/failure/timeout/cancellation recording and later
   orchestration fallback not altering provider outcome;
9. tenant and outer identity immutability/cross-tenant isolation;
10. retry recorder write without calling provider a second time;
11. the approved telemetry-write failure policy;
12. AiRequests admission/quota unchanged and no AiTokens quota effect.

Real-PostgreSQL tests (separate contexts/connections; never EF InMemory for
these guarantees):

- concurrent same-ID insertion -> one tenant row;
- concurrent unknown insert and known enrichment -> one row, known final
  tuple;
- concurrent identical known writes -> idempotent;
- conflicting known writes -> one accepted tuple and deterministic conflict;
- same ProviderCallId under another CompanyId cannot read/update the first
  tenant's row;
- two distinct IDs on one outer operation -> two rows and independently
  aggregated usage;
- cross-request/process persistence retry with same ID enriches/replays;
- no second provider invocation occurs after a telemetry persistence retry.

Do not connect this matrix to persistent `DefaultConnection`; use only an
explicitly isolated, disposable PostgreSQL test database.

### Audit safety and completion status

- Baseline, tracked worktree, staged state, and upstream sync were verified
  before review. No production/test/migration/frontend files were changed.
- No migration was generated/applied, no provider was contacted, and no
  persistent database connection was used.
- This permitted untracked report is the only file updated by this audit;
  it remains untracked. Other six reports remain untouched.
- The earlier historical Phase 2B.2 tests and build are not rerun as part of
  this read-only architecture audit.
- No fundamental Phase 2B.2 prerequisite is missing. Implementation design
  review can proceed, subject to explicit approval of telemetry write-failure
  behavior and tenant-retention/deletion policy.
