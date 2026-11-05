# Sprint 20 — Phase 2B.3 Durable AI Token Telemetry Architecture Audit

**Audit date:** 2026-10-20
**Repository:** `/workspace`
**Scope:** Read-only source and data-model audit; no implementation

> The requested filename `SPRINT20_AI_TOKEN_PERSISTENCE_AUDIT.md` also exists
> as an untracked report in the current worktree. It was not overwritten. This
> separate untracked Phase 2B.3 report is the only file updated by this audit;
> all other reports and all source files remain unchanged.

## Executive result

Phase 2B.2 is committed at `4d2108ee43cf9477d0ae206c87a7a762fbc56b31` on
`main`, equal to `origin/main`. The tracked worktree is clean; nine Markdown
reports, including this file, are untracked. The current source carries
`ProviderCallId`, optional outer identity, and runtime-only usage aggregation
through the primary provider path. It does not persist provider-call rows.

The minimum durable fact is one row per actual StayFlow provider invocation,
created before dispatch and updated with the resulting observation. A durable
outer-operation/call-role key is additionally needed to arbitrate same-call
replays before dispatch; `ProviderCallId` alone is generated anew for each
invocation and only deduplicates persistence of the same invocation ID. This
claim can prevent duplicate application dispatch, but cannot make the
provider/database boundary exactly-once. Copilot replay currently re-invokes
the provider and has no stored result to return, so its replay/API contract is
an approval blocker.

The live retrieval-diagnostics path can invoke the configured provider without
outer identity or `AiRequests` admission. Its intended paid-call classification
must also be decided before claiming complete commercial coverage.

## 1. Repository state

| Check | Result |
|---|---|
| Branch | `main`, tracking `origin/main` |
| HEAD | `4d2108ee43cf9477d0ae206c87a7a762fbc56b31` |
| Upstream | `4d2108ee43cf9477d0ae206c87a7a762fbc56b31` |
| `git status -sb` | `## main...origin/main` |
| `git status --short` | Nine untracked Markdown reports; no tracked modifications |
| Staged | None |
| `git diff --check` | Passed; no output |
| `git diff --stat` / `--name-status` | Empty |

**Phase 2B.2 is present and committed.** The current history includes
`9d8bbee`, `f4de255`, `c9becc0`, `7408874`, `6deb4c0`, and `4d2108e`; the
runtime contract and direct tests are committed. The latest commit did not
change source. Current untracked reports are not implementation inputs.

No tracked worktree changes or staged files were present. No source changes
were made during this audit.

## 2. Current Phase 2B.2 runtime contract

Evidence: [`AiTokenUsage.cs`](backend/DTOs/AIProvider/AiTokenUsage.cs),
[`AiTokenUsageAggregation.cs`](backend/DTOs/AIProvider/AiTokenUsageAggregation.cs),
[`AIProviderRequest.cs`](backend/DTOs/AIProvider/AIProviderRequest.cs),
[`AIProviderResult.cs`](backend/DTOs/AIProvider/AIProviderResult.cs),
[`AIReplyOrchestrationRequest.cs`](backend/Services/AI/Orchestration/AIReplyOrchestrationRequest.cs),
[`AIReplyOrchestrator.cs`](backend/Services/AI/Orchestration/AIReplyOrchestrator.cs).

| Concept | Current exact contract |
|---|---|
| Token usage | `AiTokenUsage(long InputTokens, long OutputTokens, long TotalTokens)`. The record constructor does not reject negative values. |
| Outer type | `OuterOperationType` enum: `GuestMessage`, `CopilotOperation`. |
| Provider call ID | `ProviderCallId` record with validated nonblank `Value`; `New()` uses a new GUID rendered as `"N"`. |
| Provider-call context | `ProviderCallContext(OuterOperationType, Guid OuterOperationId, ProviderCallId)` exists but has no production call-site usages. |
| Request/result identity | `AIProviderRequest.ProviderCallId` is non-null and defaults to `New()`; `OuterOperationType` and `OuterOperationId` are independently nullable. `AIProviderResult` has nullable `ProviderCallId`, outer type, and outer ID. |
| Orchestration identity | `AIReplyOrchestrationRequest.OuterOperationType` and `OuterOperationId` are independently nullable. Validation currently checks only `ConversationId`. |
| Runtime aggregation | `AiTokenUsageAggregation` is in-memory and keyed by `ProviderCallId.Value`. Equal known usage is idempotent; conflicting known usage throws; unknown can be enriched to known; unknown duplicate is ignored. Sums use checked arithmetic. |
| Completeness | `AiTokenUsageCompleteness` is `Unknown`, `Partial`, or `Complete`. Zero observed calls and only-unknown calls are both `Unknown`; `ProviderCallCount` distinguishes them. `Total` is null unless complete, and known zero is a non-null `0/0/0`. |
| Scope of snapshot | `AiTokenUsageSnapshot` contains outer type/ID, call count, known-call count, token totals, and completeness. It is returned by `AIReplyOrchestrationResult.TokenUsage` only when the orchestration request has both outer fields. |
| Provider outcome | `AIProviderOutcome`: `Success`, `Unavailable`, `Failed`. |

The primary provider boundary creates a fresh `ProviderCallId` immediately
before `IAIProvider.GenerateAsync`, observes its returned `TokenUsage`, and
adds known or unknown usage to the in-memory aggregation. There is no durable
call identity or usage store. `AIProviderRequest` permits an unclassified call
with no outer identity; the current request type does not require the outer
type and ID to be supplied together.

## 3. Commercial outer identities and write paths

Evidence: [`ChatService.cs`](backend/Services/ChatService.cs),
[`CopilotService.cs`](backend/Services/CopilotService.cs),
[`HostCopilotWorkspaceService.cs`](backend/Services/HostCopilot/HostCopilotWorkspaceService.cs),
[`SubscriptionEntitlementService.cs`](backend/Services/SubscriptionEntitlementService.cs).

| Path | Service and method | Outer entity before admission/provider | AiRequests admission | Provider call and ID | Usage aggregation |
|---|---|---|---|---|---|
| Guest | `ChatService.SendGuestMessageAsync` | Persists the guest `ConversationMessage` first; outer ID is `guestMessage.Data.Id`. | `ConsumeQuotaAsync(AiRequests, 1, "ai-request:guest-message:{messageId}")` precedes orchestration. | `AIReplyOrchestrator.InvokeProviderWithTimeoutAsync`; creates a new `ProviderCallId` just before provider invocation. | Orchestrator creates an aggregation for `GuestMessage` + message ID and returns its snapshot. |
| Suggestions | `CopilotService.GetSuggestedRepliesAsync` | `AdmitCopilotOperationAsync` creates/finds `CopilotOperation`; outer ID is request `operationId`. | Admission occurs before orchestration, keyed by `ai-request:copilot-operation:{operationId}`. | Same primary provider call site; runtime ID is new per invocation. | Snapshot is returned for `CopilotOperation` + operation ID. |
| Generated reply | `CopilotService.SuggestHostReplyAsync` | Same durable `CopilotOperation` admission; type `CopilotGeneratedReply`. | Same `AiRequests` operation admission. | Same primary provider call site. | Snapshot is returned for `CopilotOperation` + operation ID. |
| Workspace draft | `HostCopilotWorkspaceService.GenerateDraftAsync` | Same durable `CopilotOperation` admission; type `WorkspaceDraft`. | Same `AiRequests` operation admission. | Same primary provider call site only when `EnableLlmWording` is true. | Snapshot is available when it invokes the orchestrator. |

The four intended outer identities are present on these main commercial paths.
`CopilotOperation.OperationType` distinguishes suggestion, generated reply,
and workspace draft. The outer operation entity is persisted before provider
invocation, but it stores no provider-call ID, tokens, provider, model, or
generated result.

Two additional service paths are not represented by these expected identities:

* `CopilotService.GetRetrievalDiagnosticsAsync` calls the orchestrator without
  outer identity and without `AiRequests` admission. Its provider can be paid
  when configured for OpenAI.
* `ConversationAIExchangeService.ProcessGuestMessageAsync` is registered but
  has no caller elsewhere in the current backend source. It invokes the
  orchestrator without outer identity. Treat it as dormant/unclassified unless
  an external caller is established.

## 4. Provider invocation inventory

Classification distinguishes the provider call sites from the SDK adapter
that performs the HTTP request.

| Call site | Classification | Paid/external possible | Usage / ID / outer identity | Phase 2B.3 treatment |
|---|---|---|---|---|
| `AIReplyOrchestrator.InvokeProviderWithTimeoutAsync` → `IAIProvider.GenerateAsync` | `COMMERCIAL_OUTER` on guest and the three Copilot paths; also called by diagnostics without identity | Yes. DI selects OpenAI when `AIProvider:Provider=OpenAI`; otherwise Development. | `AIProviderResult.TokenUsage` is observed. A fresh `ProviderCallId` is created. Outer fields are only present when caller supplies both. | Persist each actual paid logical call on approved commercial paths. Resolve diagnostics separately and do not claim full coverage while unclassified. |
| `OpenAIAIProvider.GenerateAsync` → `IOpenAIResponsesClient.CreateResponseAsync` → SDK `ResponsesClient.CreateResponseAsync` | Provider adapter for the preceding invocation, not a second StayFlow logical call | Yes, OpenAI | SDK usage maps to `AiTokenUsage`; response ID maps to `RequestId`; result carries request identity. | Capture through a provider-neutral wrapper/orchestration persistence service, not inside the SDK adapter. |
| `DevelopmentAIProvider.GenerateAsync` | `DEVELOPMENT` | No; deterministic local implementation | Returns no `TokenUsage`, but result carries ProviderCallId and optional outer fields. The runtime aggregation therefore counts this as unknown, not known zero. | Persist the logical `IAIProvider` invocation with null token counts and `Provider=Development` to preserve the runtime observation; exclude it from paid-provider cost analytics. Do not infer zero from absent usage. |
| `GroundedConciergeResponseGenerator.GenerateAsync` → `IConciergeLanguageModel.GenerateAsync` | `INTERNAL_SUB_OPERATION`; current concrete provider is development-only | No, in current composition | No token usage or `ProviderCallId` in `ConciergeLanguageModelResult`; no outer identity. | Do not create paid telemetry rows for the current implementation. If an external model is later wired, it must use the same telemetry boundary or expose usage and logical identity. |
| `AIOrchestrator.ProcessAsync` → `IAIProvider.GenerateAsync` | `LEGACY` | Technically yes via DI, but its only HTTP controller, `AIOrchestrationController`, returns 410; no other production caller was found. | `AIProviderRequest` generates a default ID, but no outer identity is set. The legacy result/audit path does not expose or persist token usage. | Keep excluded while endpoint is 410 and service has no caller; if reactivated, add classification and telemetry before allowing paid calls. |
| `ConversationAIExchangeService.ProcessGuestMessageAsync` → `IAIReplyOrchestrator.OrchestrateAsync` | `DORMANT` / unclassified service path | Yes if invoked and configured for OpenAI | No outer identity is set; the inner primary provider ID exists at runtime; usage snapshot is not propagated by its result mapper. No call site in current backend source. | Confirm whether it is intentionally dead. If enabled, add durable outer identity/admission and telemetry. |
| `CopilotService.GetRetrievalDiagnosticsAsync` → `IAIReplyOrchestrator.OrchestrateAsync` | `UNKNOWN` (live diagnostics surface, not an outer commercial operation) | Yes if configured for OpenAI | No `AiRequests` admission, outer identity, or returned usage field; internal call gets a runtime ID but aggregate is null. | Product/architecture must classify or remove/disable the paid provider call before asserting complete coverage. |
| `IReservationLifecycleEventGenerator.GenerateAsync` | `NON_PROVIDER` | No evidence of LLM use; it is not an `IAIProvider` or language-model implementation | Not applicable | Exclude from token persistence. |

The full repository search found only the two direct `IAIProvider.GenerateAsync`
call sites above (`AIReplyOrchestrator` and legacy `AIOrchestrator`), one
`IConciergeLanguageModel.GenerateAsync` call site, and the OpenAI response
adapter. The development implementations are selected/registered in
[`ServiceCollectionExtensions.cs`](backend/Extensions/ServiceCollectionExtensions.cs).

## 5. Grounded concierge call

Evidence: [`GroundedConciergeResponseGenerator.cs`](backend/Services/AI/Orchestration/GroundedConciergeResponseGenerator.cs),
[`ConciergeLanguageModelProviderFactory.cs`](backend/Services/AI/Orchestration/ConciergeLanguageModelProviderFactory.cs),
[`DevelopmentConciergeLanguageModel.cs`](backend/Services/AI/Orchestration/DevelopmentConciergeLanguageModel.cs),
[`ConciergeLanguageModelResult.cs`](backend/Services/AI/Orchestration/ConciergeLanguageModelResult.cs).

| Question | Finding |
|---|---|
| Concrete `IConciergeLanguageModel` | `DevelopmentConciergeLanguageModel`; the factory resolves this same implementation whether `GroundedConciergeOptions.Enabled` is true or false. |
| Classification | `DEVELOPMENT_ONLY` in current source. It performs deterministic local response construction, not an external model call. |
| Paid/external possible now | No. No production external implementation is registered or selected. |
| Provider/model | Result labels are `"Development"` / `"development"`. |
| SDK usage | None; no SDK call. |
| Token usage discarded | There is no usage value to collect; `ConciergeLanguageModelResult` has no token fields. |
| Logical call identity | No `ProviderCallId`; result `RequestId` is a generated development diagnostic ID, not the StayFlow provider-call identity. |
| Outer identity | Not passed to `IGroundedConciergeResponseGenerator` or `IConciergeLanguageModel`. |
| Separate persisted row | Not for the current implementation. If this interface becomes externally backed, it must become observable and receive a distinct stable logical-call key under the same outer operation rather than being silently omitted. |
| Coverage limitation | Current code is not an unobservable paid call. Future paid coverage cannot be claimed until the concrete implementation and usage propagation change. |

## 6. Existing persistence inventory and usage boundary

Evidence: [`ApplicationDbContext.cs`](backend/Data/ApplicationDbContext.cs),
[`UsageRecord.cs`](backend/Models/UsageRecord.cs),
[`UsageOperation.cs`](backend/Models/UsageOperation.cs),
[`CopilotOperation.cs`](backend/Models/CopilotOperation.cs),
[`ConversationMessage.cs`](backend/Models/ConversationMessage.cs).

| Entity/table | Current purpose and relevant fields | Why it is not the provider-call fact table |
|---|---|---|
| `UsageRecord` / `UsageRecords` | `Id`, `CompanyId`, metric, billing-period start/end, quantity; unique `(CompanyId, Metric, PeriodStartUtc)`. Company FK cascades. | Period-level quota usage aggregation; no call ID, outer ID, model/provider, outcome, or raw token triple. |
| `UsageOperation` / `UsageOperations` | `Id`, `CompanyId`, metric, idempotency key, period start, quantity; unique `(CompanyId, Metric, PeriodStartUtc, IdempotencyKey)`. Company FK cascades. | Idempotent quota admission operation, not provider invocation telemetry. |
| `CopilotOperation` / `CopilotOperations` | PK `Id`; company, conversation, actor, operation type, status, created/completed times. FKs to Company, Conversation, User use `Restrict`. | One user/business operation can invoke zero or multiple provider calls; it has no provider/model/token fields or cached generated response. |
| `ConversationMessage` / `ConversationMessages` | Company/conversation IDs, text, sender/type, external and message idempotency IDs, provider name/model/request ID, AI outcome/failure fields, sent/deleted times. Soft-deleted and query-filtered. Company FK restricts; conversation FK cascades. | Message is not the provider call; it stores content and has no token usage or call ID. Multiple calls can relate to a single guest message. |
| `AuditLog` / `AuditLogs` | Operational/business audit trail with entity/action/details; the current entity has no `CompanyId`. | No raw provider usage contract; do not overload it with token facts. |
| `ActionNotificationOutbox` | Notification delivery state, attempts, next retry, failure code and payload reference. | Specific notification-delivery outbox, not a generic AI telemetry persistence pattern. |

No existing database entity or DbSet represents AI token usage, provider calls,
or durable AI telemetry. The current provider/model/request/failure fields on
messages are not a reusable row because they are at message granularity and
do not contain tokens.

**UsageRecord suitability: No. UsageOperation suitability: No.** Source proves
these are quota constructs: `ConsumeQuotaAsync` consumes `AiRequests` for guest
messages and Copilot operations, with idempotency keys. `UsageMetric.AiTokens`
exists as an entitlement/metric and is seeded in plans, but there is no current
`ConsumeQuotaAsync(UsageMetric.AiTokens, ...)` call. `AiTokens` remains
telemetry-only. Do not call quota consumption for tokens.

## 7. Migrations and EF model status

Migration source: [`20260922185101_AddCopilotOperations.cs`](backend/Data/Migrations/20260922185101_AddCopilotOperations.cs).

* Latest migration source is `20260922185101_AddCopilotOperations`; no later
  migration exists in `backend/Data/Migrations`.
* `AddCopilotOperations` and its designer are tracked/committed.
* The migration creates `CopilotOperations` with Company, Conversation, and
  User FKs (`Restrict`) and operation indexes.
* Applied status was **not inspected**: that requires a persistent database,
  which this audit did not access.
* Safe model check run:
  `dotnet ef migrations has-pending-model-changes --project backend/backend.csproj --startup-project backend/backend.csproj`
  **Passed:** “No changes have been made to the model since the last migration.”
  This built the project and compared EF metadata; it did not apply a migration
  or query migration state.

## 8. Persistence unit and logical idempotency

**Recommended unit: one row per actual StayFlow provider invocation**—one
`IAIProvider.GenerateAsync` invocation as understood by StayFlow. This is not
one row per hidden SDK/network retry because the current adapter does not
expose those attempts or individual usage. A deliberate retry that invokes the
provider again is a separate invocation and must not overwrite its predecessor.

Alternatives rejected:

* One row per outer operation or per `ConversationMessage` / `CopilotOperation`
  is too coarse: an operation can have zero calls, and distinct logical calls
  must be representable.
* One row per aggregate loses per-call known/unknown facts and replay
  enrichment.
* One row per SDK attempt cannot be populated accurately from the current SDK
  abstraction.

`ProviderCallId` is generated in memory immediately before the current primary
provider invocation. A telemetry-write retry within that invocation can reuse
it, but a process/request replay creates a new ID. A unique `ProviderCallId`
constraint deduplicates observations for one invocation; it does not suppress
a repeated outer operation.

To suppress a duplicate application dispatch, add a stable `LogicalCallKey`
for the outer operation and named provider-call role, claimed atomically
before dispatch. Current commercial orchestration has one primary provider
role; future distinct stages need distinct keys. An explicitly authorized
retry uses a distinct attempt/key and is a new persisted invocation. A unique
logical key alone cannot both preserve every retry attempt and represent a
single forever-idempotent operation unless retry policy and the logical
attempt boundary are explicit.

Do not derive keys from prompt text, request correlation IDs, or random
per-process values. Bind key reuse to the existing durable operation identity
and immutable request contract; do not add prompt/response content or an
unreviewed prompt hash to telemetry.

| Case | Required behavior |
|---|---|
| Same `ProviderCallId` replay | Return the existing row if observations agree; enrich only unknown usage with known usage; reject conflicting known observations. |
| Same outer operation, same logical call | A durable atomic claim prevents a second application dispatch; a duplicate sees the existing row/state. |
| Same outer operation, distinct legitimate call | A distinct `LogicalCallKey` creates a distinct row; do not put a unique constraint on outer identity alone. |
| Deliberate new call/retry | Use an explicitly distinct logical-call key/attempt ordinal according to an approved retry policy; retain both invocation rows. |

## 9. Exact replay behavior

Evidence: `ChatService.SendGuestMessageAsync`, Copilot admission callers, and
`SubscriptionEntitlementService.AdmitCopilotOperationCoreAsync`.

| Path | Reinvocation / short-circuit |
|---|---|
| Guest | If `ExternalMessageId` is present and a matching persisted message is found, `ChatService` returns early before quota admission and provider invocation. Without that external ID, this method has no request-level idempotency key and can persist a new guest message on replay, producing a new outer ID and another provider call. The guest row is persisted before admission/provider. A retry after matching-message persistence short-circuits rather than resuming generation; a crash after provider success but before assistant-message persistence can therefore avoid duplicate spend but leave no generated reply. |
| Suggestions | `AdmitCopilotOperationAsync` finds the same `CopilotOperation` and idempotently meters the request, but `GetSuggestedRepliesAsync` ignores the returned replay flag and still calls `OrchestrateAsync`. The operation ID is stable only if the client reuses it; `ProviderCallId` is regenerated. |
| Generated reply | Same behavior in `SuggestHostReplyAsync`: the same admitted operation ID does not short-circuit orchestration; a replay can invoke the provider again. |
| Workspace draft | Same behavior in workspace draft generation when LLM wording is enabled. The same operation ID/status is reused, but there is no provider-dispatch replay gate. |
| Diagnostics | No outer operation ID, idempotent call key, or `AiRequests` admission is supplied. |

**ProviderCallId stable across process replay: No.** A database uniqueness
constraint on `ProviderCallId` alone is insufficient to suppress another
provider dispatch. A durable logical-call claim must be checked before
dispatch. `CopilotOperation` persists status, not the generated
suggestion/reply/draft, so suppressing dispatch leaves no original result to
return. The API replay contract must define whether completed replay returns a
separately stored business result, an explicit already-completed response, or
requires a new operation ID. Never store generated content in telemetry.

## 10. Crash windows and minimal call lifecycle

Use explicit call state, not a claim of a cross-system transaction. A compact
state set is `Prepared`, `Invoking`, `Succeeded`, `Unavailable`, `Failed`.
`Succeeded` with all token fields null means success with unknown usage;
`Succeeded` with `0/0/0` means known zero. Failure category and token
nullability further describe terminal rows. A stale `Invoking` row is
potentially abandoned and usage-unknown, not proof the provider was not called.

| Window | Desired durable meaning |
|---|---|
| A. Row created, process crashes before provider invocation | `Prepared`; known not dispatched by the application. Do not count it as an observed provider call. |
| B. Provider returns known usage, crash before telemetry update | `Invoking`, all token fields null: call may have happened and usage is unknown to durable storage. Never claim complete coverage. |
| C. Provider succeeds, outer business operation later fails | Preserve terminal provider success and its token facts. Provider telemetry is independent from Copilot operation/business result status. |
| D. Timeout/exception | `Unavailable` or `Failed`, category only, token triple null unless the provider actually returned a usage observation. Do not store exception text. |
| E. Telemetry persisted, outer operation replays | Match the logical-call key before invoking; return/reconcile the existing row and do not overwrite known facts with null. Existing Copilot output replay still needs a separate API/business decision. |

`Prepared` should be committed before external dispatch; `Invoking` should be
committed immediately before the provider call. There is still an unavoidable
gap between the latter commit and the external request, so an `Invoking` row
cannot prove whether the provider accepted the request.

## 11. Token nullability and completeness

Persist the raw per-call facts and derive aggregate completeness. Do not store
redundant aggregate totals/completeness unless a separate proven query need
emerges.

| Observation | `InputTokens`, `OutputTokens`, `TotalTokens` | Aggregate meaning |
|---|---|---|
| Usage unknown/not returned | `NULL`, `NULL`, `NULL` | Unknown; not zero. |
| Known zero | `0`, `0`, `0` | Known zero. |
| Known usage | All non-null and non-negative | Complete for that call. |
| Mixed known and unknown calls | Some rows have counts, others all null | Partial, derived for the selected operation/window. |
| No dispatched/observed provider rows | No usage row (or only `Prepared`) | Zero observed calls; keep count separate from `Unknown` completeness. |

Recommend a check constraint enforcing either all three token columns null or
all three non-null, plus non-negative values when known. Current
`AiTokenUsage` does not enforce non-negative values; any Phase 2B.3 boundary
validation must be deliberate and consistent with this database constraint,
not a silent constructor change. No aggregate-completeness column is needed;
exclude `Prepared` rows and derive counts/totals/completeness from dispatched
and terminal call rows.

## 12. Provider/model/request/outcome metadata

Evidence: [`OpenAIAIProvider.cs`](backend/Services/OpenAIAIProvider.cs),
[`OpenAIResponsesClient.cs`](backend/Services/OpenAIResponsesClient.cs),
[`OpenAIProviderContracts.cs`](backend/Services/OpenAIProviderContracts.cs).

* **Provider:** Known from selected provider configuration before invocation
  (`OpenAI` or `Development`) and returned as `AIProviderResult.ProviderName`.
  Persist this source value, not an exception message.
* **Model:** Configured before invocation (`OpenAIOptions.Model`, currently a
  configurable model with a default); result exposes `ModelName`. Current
  `OpenAIResponsesClient` sets `ModelName` to the requested model and does not
  read an actual/resolved model from `ResponseResult`. A field sourced from
  this code must therefore be understood as the configured/requested model,
  not guaranteed provider-resolved model. Verify provider behavior before
  treating it as exact historical pricing identity in Phase 2C.
* **Provider request/response ID:** `OpenAIResponsesClient` maps
  `ResponseResult.Id` to `OpenAIProviderResponse.RequestId`, then
  `AIProviderResult.RequestId`. Store optionally as diagnostic metadata only;
  it is not a StayFlow idempotency key. Failures often have no ID.
* **Outcome:** Persist current categorical `Success`, `Unavailable`, or
  `Failed` (or corresponding durable state); no response text.
* **Failure category:** Existing categories include timeout, cancellation,
  network, rate-limited, provider-server-error, authentication,
  invalid-request, unexpected-response, empty-response, and unknown. Persist
  only bounded categorical values; never exception messages or stacks.
* **Duration:** `DurationMs` exists in provider result but is not required for
  the minimal token/cost fact. It may be added only if operational analytics
  explicitly need it.

Do not add pricing, currency, cost, markup, invoice amount, cost allocation,
or price-version fields; those belong to Phase 2C.

## 13. Recommended entity and migration design

**Provisional entity/table:** `AiProviderCall` / `AiProviderCalls`, extending
`AuditableEntity`. This names each observed StayFlow provider invocation, not
the outer Copilot/guest operation.

| Field | Type / nullable | Source and purpose | Constraint/index |
|---|---|---|---|
| `Id` | `Guid`, required | Row primary key. | PK. |
| `CompanyId` | `Guid`, required | Trusted tenant context passed by orchestration. Current data access uses explicit `CompanyId` predicates; there is no global tenant query filter. | FK to `Company`; recommend `Restrict` pending retention policy. Prefix tenant indexes. |
| `ProviderCallId` | bounded string, required | `AIProviderRequest.ProviderCallId.Value`; identifies one runtime provider invocation and supports same-ID observation retries. `ProviderCallId` is not the vendor `ProviderRequestId`. The current value object rejects blank values but does not enforce a storage length; `New()` emits GUID-N. | Global unique; every read/update still scopes by `CompanyId`. Validate the bounded persisted form at the boundary and treat cross-company collision as an invariant violation. |
| `OuterOperationType` | enum/string, nullable | Current optional request outer type. | Both outer fields null or both non-null; restrict to known enum values. |
| `OuterOperationId` | `Guid`, nullable | Message ID or `CopilotOperation.Id`. | Paired with outer type; index for correlation. No polymorphic FK. |
| `LogicalCallKey` | bounded string, required for a correlated commercial call; nullable only for an explicitly approved uncorrelated call | Stable named call role/attempt key reused on replay. No current value exists in the provider contract. | Partial unique `(CompanyId, OuterOperationType, OuterOperationId, LogicalCallKey)` when outer identity is present. Replays find the existing claim before dispatch; an approved new attempt has a distinct key. |
| `Provider` | bounded string, required when dispatch is prepared | Selected provider descriptor (for example `OpenAI` or `Development`), not synthetic wrapper labels such as `Exception` or `TimedOut`. | Include in provider/model analytics index only if query needs it. |
| `RequestedModel` | bounded string, nullable until correctly known | Configured/requested model. Current adapter does not expose the provider-resolved model. | Do not treat as a resolved model or exact historical price identity. |
| `InputTokens` | `long`, nullable | `AiTokenUsage.InputTokens`. | All three usage fields null or all non-null; non-negative check. |
| `OutputTokens` | `long`, nullable | `AiTokenUsage.OutputTokens`. | Same constraint. |
| `TotalTokens` | `long`, nullable | `AiTokenUsage.TotalTokens`. | Same constraint. |
| `State` | bounded enum/string, required | Persistence lifecycle: `Prepared`, `Invoking`, `Succeeded`, `Unavailable`, `Failed`. | Required, constrained to supported values. |
| `FailureCategory` | bounded string, nullable | Existing categorized provider failures only. | Null on success; bounded/allowlisted values. |
| `ProviderRequestId` | bounded string, nullable | OpenAI response `Id`; optional diagnostic correlation, never primary identity. | Optional, no uniqueness. |
| `InvokedAt` | `DateTimeOffset`, nullable | Time dispatch starts. Null for `Prepared`. | Optional period/diagnostic use. |
| `CompletedAt` | `DateTimeOffset`, nullable | Terminal observation time. Null while prepared/invoking. | Optional. |
| `CreatedAt`, `UpdatedAt` | `DateTimeOffset`, required | Existing `AuditableEntity` persistence convention. | Created timestamp for period analytics; updated on enrichment. |

There must be no persisted prompt, response, guest message, phone/email, API
key, access token, exception text, or stack trace. `CompanyId`, opaque outer
IDs, bounded provider/model names, categorical failure code, and optional
provider response ID are the only operational metadata proposed.

### Constraints and indexes

1. PK on `Id`.
2. FK `CompanyId → Companies.Id`; recommend `Restrict` so hard company deletion
   cannot silently erase historical telemetry. Product retention policy must
   confirm this.
3. Global unique `ProviderCallId` for same invocation observation dedupe; retain
   `CompanyId` in every scoped lookup/update.
4. Partial unique `(CompanyId, OuterOperationType, OuterOperationId,
   LogicalCallKey)` when both outer fields are non-null. This is the durable
   same-logical-call replay key and permits multiple calls per outer operation.
5. Check outer type and ID are either both null or both non-null; correlated
   commercial rows require a nonblank logical key. Uncorrelated rows require
   an explicitly approved internal classification and replay policy.
6. Check token triple is all-null or all-non-null; known values are
   non-negative.
7. Minimal analytics indexes: `(CompanyId, CreatedAt)` for tenant/period
   queries and the logical-call unique index for outer-operation correlation.
   Add provider/model/time indexes only after a measured query need.

Do **not** add `UNIQUE(OuterOperationType, OuterOperationId)` alone. Do not use
the provider request ID as an idempotency key.

### Foreign keys, ownership, deletion, and backfill

Use explicit `CompanyId` with a Company FK; it avoids tenant ownership being
implicit through a polymorphic parent and supports the explicit tenant
predicates used by current services. `ApplicationDbContext` also validates
tenant ownership on writes when authenticated tenant context is available;
this is not a substitute for scoping every telemetry read by `CompanyId`.
Do not FK `OuterOperationId` directly: it polymorphically refers to either
`ConversationMessage` or `CopilotOperation`, so one ordinary FK cannot express
it. Two nullable parent FKs or a new shared AI-operation entity add complexity
not justified by this audit. Validate outer ownership at the trusted write
boundary; retain outer type/ID as correlation only.

Conversation, message, and property deletion is currently predominantly
soft-delete/query-filter behavior. `Conversation.PropertyId` uses `Restrict`;
message-to-conversation uses `Cascade`; CopilotOperation-to-Conversation and
CopilotOperation-to-Company use `Restrict`. No hard-delete service was found
for these commercial entities. A telemetry row without parent FKs survives
soft deletion and any permitted parent deletion, preserving analytics.
Company hard-delete behavior for historical telemetry is a product retention
decision. No retention policy is inferred here.

**Backfill:** none. No existing row stores provider-call IDs or per-call token
counts. Conversation message provider metadata and period-level `UsageRecord`
quantities cannot reconstruct reliable per-call token observations.

## 14. Write boundary and persistence-failure policy

Persist in a provider-neutral orchestration/telemetry service that surrounds
the provider invocation and receives tenant, outer identity, logical key,
provider/model descriptor, and the result. `AIReplyOrchestrator` is the
current primary boundary: it has the invocation, usage result, call ID, and
outer identity. Keep EF/database work out of `OpenAIAIProvider` and
`OpenAIResponsesClient`. The legacy invocation must remain blocked or be
explicitly brought under the same policy if it is reactivated.

1. Validate tenant/correlation, create the stable logical-call claim, and
   commit `Prepared` before provider dispatch. If this persistence fails, do
   not make a paid provider call.
2. Use an atomic compare-and-set transition `Prepared` → `Invoking`. Only the
   request that wins this transition may dispatch. A duplicate seeing an
   existing `Invoking` or terminal row must not dispatch again. `Invoking`
   means the provider may have received the request; never auto-replay it as
   though it were definitely unsent.
3. On provider return, persist success/failure, token triple (known or all
   null), request ID, and categorical failure metadata. Retry telemetry
   persistence using the same row and `ProviderCallId`; never retry the
   external provider merely because telemetry persistence failed.
4. If a successful provider response cannot be durably recorded after bounded
   persistence retries, preserve the customer-facing successful result rather
   than failing the operation and encouraging a duplicate paid retry. Emit an
   explicit high-severity telemetry-gap signal/alert; the committed `Invoking`
   row remains evidence of unknown usage if completion cannot be written.
5. Existing source has no generic telemetry retry/outbox pattern. The
   notification-specific `ActionNotificationOutbox` is not a direct reuse.
   This is an explicit operational policy recommendation, not an existing
   repository behavior.

## 15. Concurrency requirements

* Same `ProviderCallId` concurrently: database unique constraint; reload the
  existing row and apply same-observation/enrichment/conflict rules.
* First-row creation race: logical-key unique constraint; only the expected
  unique violation is handled as a race; reload and reconcile rather than
  swallowing arbitrary database failures.
* Unknown → known concurrently: atomic update/row lock or serializable
  transaction; known observation enriches null; equivalent known replay is
  no-op; conflicting known values are rejected.
* Same outer replay concurrently: only the request that atomically transitions
  the durable claim from `Prepared` to `Invoking` may dispatch. A second
  request must not dispatch again. A crash after `Invoking` is ambiguous
  because provider and DB are not atomic; do not automatically retry it under
  the same key.
* Two legitimate calls under one outer operation: different logical keys;
  both rows are retained.
* Do not use EF InMemory as concurrency evidence. Existing quota service uses
  relational transactions/retry behavior, but telemetry needs tests against
  PostgreSQL itself.

### Future isolated PostgreSQL validation matrix (not run)

1. Concurrent same `ProviderCallId` insert; assert one row and replay behavior.
2. Concurrent unknown → same known enrichment; assert final single known
   observation.
3. Concurrent conflicting known observations; assert explicit conflict and
   no last-write-wins overwrite.
4. Concurrent same logical outer key; assert only one provider-dispatch claim.
5. Two distinct logical keys with the same outer identity; assert two rows.
6. First-row unique-constraint race and recovery/reload behavior.
7. Serializable/deadlock/serialization retry if the chosen implementation
   uses transactional claim semantics.

This matrix is future work, not completed deployment validation.

## 16. Minimum analytics enabled

* Tokens by company and current billing period, reporting known totals and
  unknown/in-flight counts separately.
* Calls and tokens by `OuterOperationType` + `OuterOperationId`.
* Calls/tokens by provider/model and time range.
* Unknown/partial telemetry rate, with `Prepared` excluded and `Invoking`
  separately visible.
* Provider failure/unavailable rate by provider/model and failure category.

No UI, analytics endpoint, pricing, currency, cost, markup, invoice amount,
allocation, or historical price-version work is included in Phase 2B.3.

## 17. Failure and replay test plan before implementation

### Unit tests

* Known non-zero usage and known zero (`0/0/0`).
* Unknown usage remains null and distinct from known zero.
* Zero observed calls distinct by count from all-unknown calls.
* Same call ID and same known usage is idempotent.
* Unknown → known enrichment.
* Conflicting known usage rejects without overwrite.
* Mixed known/unknown derives `Partial`; all known derives `Complete`.
* Checked aggregation overflow remains rejected.
* Negative usage policy is explicit and consistent with database constraints.

### Persistence tests

* Create `Prepared`, transition to `Invoking`, and write each terminal state.
* Same `ProviderCallId` replay; same known replay; unknown → known; conflict.
* Duplicate logical key resolves as same logical call; distinct logical keys
  on one outer operation remain distinct.
* Tenant-scoped read/write isolation and Company FK behavior.
* Outer identity pairing and correlation validation.
* Nullable token triple and non-negative constraints.
* Preserve row through outer business failure and soft deletion.

### Commercial integration tests

* Guest path persists `GuestMessage` + persisted guest message ID.
* Suggestions persists `CopilotOperation` + operation ID.
* Generated reply persists `CopilotOperation` + operation ID.
* Workspace draft persists `CopilotOperation` + operation ID when LLM wording
  is enabled; deterministic mode does not invent a provider call.
* Each provider call ID remains distinct from outer operation ID.
* Diagnostics path has approved disposition before claiming complete
  commercial coverage.

### Failure/replay tests

* Provider failure records outcome/category with unknown usage.
* Provider response with known usage and later business-operation failure
  retains known usage.
* Persistence failure before invocation prevents provider dispatch.
* Persistence failure after provider success retries persistence, never
  re-invokes provider, and surfaces telemetry-gap alert if unresolved.
* Crash-window representation for `Prepared` and stale `Invoking`.
* Guest duplicate external message ID and no-external-ID replay behavior.
* Copilot duplicate/replay does not generate another call after replay policy
  and response behavior are approved.

### PostgreSQL concurrency tests

Run the separate matrix in section 15 against an isolated PostgreSQL database.
Do not mark it complete from EF InMemory or from the audit.

## 18. Phase 2A / 2B regression protection

* Preserve `AiRequests` admission before paid provider dispatch and existing
  quota idempotency keys; do not meter tokens through `UsageMetric.AiTokens`.
* Preserve `CopilotOperation` creation and lifecycle (`Pending`,
  `Completed`/`Failed`) and do not conflate it with provider-call state.
* Preserve quota exhaustion and provider-failure charging semantics: request
  admission remains independent from token telemetry.
* Preserve Guest/Suggestions/Generated reply/Workspace outer IDs and keep
  ProviderCallId distinct.
* Preserve zero calls vs unknown usage, known zero vs unknown, duplicate known
  idempotency, unknown-to-known enrichment, known conflict rejection, and
  overflow rejection.
* Keep `POST /ai/orchestrate` returning 410.

## 19. Security and privacy

| Data | Persist? |
|---|---|
| Prompt or rendered messages | No |
| Provider response / guest message content | No |
| Phone numbers or email addresses | No |
| API key/access token | No |
| Exception text / stack traces | No |
| Provider/model categorical names | Yes, bounded and non-secret |
| Failure category | Yes, bounded categorical value only |
| Provider response ID | Optional diagnostic metadata only |
| Company and opaque outer IDs | Yes, needed for tenant isolation and correlation; no content |

## 20. Open design questions and blockers

1. **Copilot replay contract (blocking):** operation ID is idempotent for quota
   and `CopilotOperation` creation, but not provider dispatch. A completed
   operation has no stored result to return. Decide whether replay returns a
   cached business result (stored outside telemetry), an explicit
   already-completed response, or a new operation ID is required.
2. **Diagnostics paid call (blocking for complete coverage):** decide whether
   `GetRetrievalDiagnosticsAsync` is allowed to call a paid provider. If yes,
   give it admission and a durable outer/logical identity or explicitly
   classify it as excluded from commercial token coverage.
3. **Dormant exchange service / legacy service:** confirm whether either is
   intentionally retained. If reactivated, add outer identity and telemetry
   before OpenAI use.
4. **Provider model identity for Phase 2C:** current OpenAI adapter echoes
   requested model, not provider-resolved model. Confirm whether configured
   model is pinned/immutable enough for historical pricing or expose the
   actual response model.
5. **Hard-delete retention:** confirm Company hard-delete and historical
   telemetry retention behavior; current source does not define a product
   retention policy.
6. **Usage validation:** decide how the current constructor's acceptance of
   negative token values is reconciled with database non-negative checks.

## 21. Files and audit safety

| Item | Result |
|---|---|
| Audit report | `SPRINT20_AI_TOKEN_PERSISTENCE_AUDIT_PHASE_2B_3.md`, one of nine untracked Markdown reports; updated in place |
| Other untracked reports | Eight, left unchanged; all nine are Markdown, none is implementation source/test code |
| Tracked source/test files modified | No |
| Migration generated/applied | No |
| Staged / committed / pushed | No |
| Tests run | No; audit-only task |
| EF pending-model check | Passed; no pending changes |

## Final assessment

The recommended storage grain is an invocation row with a durable pre-dispatch
claim keyed by tenant, outer identity, and named logical-call role/attempt.
Persist null token triples as unknown, retain known zero distinctly, and store
provider-reported totals without assuming they equal input plus output.
Preserve provider-call facts independently from outer-operation success,
fallback, or later business persistence. This provides durable observation
and at-most-one application dispatch for a claimed logical attempt—not
exactly-once provider execution.

The current contracts expose enough facts for the primary OpenAI call, subject
to the requested-versus-resolved model caveat. Persistence design approval
remains blocked on two product/architecture decisions: Copilot replay currently
re-invokes the provider without a durable business result or dispatch gate;
and the live retrieval-diagnostics route can call the configured provider
without outer identity or `AiRequests` admission. Dormant/legacy paths,
retention, and negative-usage validation also require explicit treatment
before their respective scopes are enabled.

**NOT READY — approve the Copilot operation replay contract and retrieval-diagnostics paid-call disposition before persistence implementation.**
