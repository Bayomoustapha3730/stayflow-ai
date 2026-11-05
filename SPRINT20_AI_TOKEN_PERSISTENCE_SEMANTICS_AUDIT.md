# Sprint 20 — Phase 2B.3 Durable Telemetry Semantic-Boundary Audit

**Audit date:** 2026-10-20  
**Repository:** `/workspace`  
**Mode:** Read-only source and architecture audit; this report is untracked.  
**Baseline:** `6deb4c09b80f7354f4f5b5555d41a8d47131e4c6`

## Executive result

Phase 2B.2 is committed and present at the requested baseline. Its
`ProviderCallId` is generated for a runtime `IAIProvider.GenerateAsync`
invocation, not persisted and not stable across HTTP replay or process restart.
It therefore identifies an observed StayFlow provider invocation (an
application-visible attempt), not a durable logical call.

The durable telemetry boundary should represent both:

1. a **logical provider call**, stable for the same intended work across
   request replay; and
2. each **StayFlow-visible provider invocation attempt** made for that logical
   call.

The current Copilot endpoints reuse the same `CopilotOperation.Id` and avoid a
second `AiRequests` charge on replay, but still invoke orchestration again.
The operation stores no generated response or payload fingerprint. A replay
can therefore spend again and return a different result. Recommend that
replaying the same operation with the same request semantics represents the
same logical provider call and is dispatched at most once by the application.
A deliberate regeneration should use a new operation ID. This is a
recommendation, not an existing source guarantee; the result/error behavior
for completed, failed, and in-progress replays still needs product/API
approval.

Retrieval diagnostics is a live authenticated Copilot service path that can
reach the same primary provider without an outer identity or `AiRequests`
admission. Its entitlement check is not request quota admission. It must be
classified before durable commercial coverage is claimed.

**Migration readiness: NO.** The contract recommendation is sufficiently
specific to guide decisions, but the Copilot replay response/recovery policy,
diagnostics billing classification, and concurrent/crash recovery policy
remain approval blockers. This audit proposes semantics, not schema approval
or implementation.

## 1. Baseline and current Phase 2B.2 contract

Verified source at the stated baseline:

| Item | Current behavior |
|---|---|
| `ProviderCallId` | StayFlow-owned nonblank GUID string (`"N"` format), carried in `AIProviderRequest` and result metadata. |
| Creation | `AIReplyOrchestrator.InvokeProviderWithTimeoutAsync` creates a new ID immediately before calling `IAIProvider.GenerateAsync`. It is not persisted. |
| Scope | One application-level `IAIProvider.GenerateAsync` invocation. It does not expose or identify internal HTTP/SDK attempts. |
| Outer identity | Optional `OuterOperationType` and `OuterOperationId`, independently nullable in request DTOs; commercial callers supply both. |
| Token usage | Nullable `AiTokenUsage(InputTokens, OutputTokens, TotalTokens)`. Null means unknown; `0/0/0` is a known observation. |
| Aggregation | Runtime-only dictionary/set keyed by `ProviderCallId`; no database persistence. |
| Completeness | `Unknown`, `Partial`, `Complete`. Count distinguishes no observed calls from observed calls with unknown usage. |
| Failure observation | The orchestrator records unknown usage on thrown exceptions/cancellation; returned provider failures can include known usage if the provider returned it. |
| Fallback | Deterministic fallback does not invent token usage. The primary provider attempt is observed before the caller constructs/returns fallback output. |
| Durable replay identity | None for provider calls. `ProviderCallId` changes on a later orchestration invocation. |

The relevant implementations are `backend/DTOs/AIProvider/AiTokenUsageAggregation.cs`,
`backend/DTOs/AIProvider/AIProviderRequest.cs`,
`backend/DTOs/AIProvider/AIProviderResult.cs`, and
`backend/Services/AI/Orchestration/AIReplyOrchestrator.cs`.

## 2. Provider dispatch inventory

`IAIProvider.GenerateAsync` has two production call sites: the current primary
orchestrator and the legacy orchestrator. The OpenAI adapter's
`ResponsesClient.CreateResponseAsync` is the downstream external request, not
a second StayFlow logical call. The grounded concierge abstraction is another
language-model-shaped path, but the current factory always returns the local
development implementation.

| Path and source | Caller / abstraction | Classification and identity | Admission / repeatability | Usage, failure, fallback |
|---|---|---|---|---|
| `ChatService.SendGuestMessageAsync` → `AIReplyOrchestrator.OrchestrateAsync` → `IAIProvider.GenerateAsync` | Guest chat; `IAIProvider` resolves to OpenAI or development based on `AIProvider:Provider`. | Commercial. Outer is persisted guest `ConversationMessage.Id`, type `GuestMessage`; tenant comes from authenticated/validated `companyId`. | `AiRequests` admission key is `ai-request:guest-message:{messageId:N}`. Matching external-message replay returns early if the external ID is supplied; without it, a repeated HTTP request can create a new message and new outer operation. | Runtime usage and result/failure observed by the orchestrator. Safe deterministic fallback is used for unavailable/failed generation. |
| `CopilotService.GetSuggestedRepliesAsync` → orchestrator → provider | Suggestions; primary provider abstraction. | Commercial. Outer is durable `CopilotOperation.Id`, type `CopilotOperation`. | Admission key `ai-request:copilot-operation:{operationId:N}`. Same ID is validated and not charged twice, but service ignores replay result and calls orchestration again. | Runtime usage observed; failure/fallback is reflected in operation state and response. |
| `CopilotService.SuggestHostReplyAsync` → orchestrator → provider | Generated reply; primary provider abstraction. | Commercial. Outer is durable `CopilotOperation.Id`. | Same quota key/admission behavior; same-ID replay redispatches. | Runtime usage observed; failure/fallback updates operation status. |
| `HostCopilotWorkspaceService.GenerateDraftAsync` → orchestrator → provider | Workspace draft; primary provider abstraction when `EnableLlmWording` is true. | Commercial. Outer is durable `CopilotOperation.Id`. | Same quota admission. Same-ID replay redispatches when LLM wording is enabled; deterministic-only mode does not invoke the provider. | Runtime usage observed on the LLM path; provider failure can result in deterministic draft fallback. |
| `CopilotService.GetRetrievalDiagnosticsAsync` → orchestrator → provider | Authenticated diagnostics endpoint; same primary abstraction. | Live diagnostics surface, not currently classified as a commercial outer operation. Has tenant `companyId` but no outer identity. | Checks Host Copilot feature, but does not call `AdmitCopilotOperationAsync` or consume `AiRequests`. Repeated endpoint calls may redispatch. | The orchestrator's aggregation is null without outer identity, so token usage is not returned/persisted through this caller. Orchestrator fallback still applies. |
| `ConversationAIExchangeService.ProcessGuestMessageAsync` → orchestrator → provider | DI-registered service; no backend caller found. | Dormant/unclassified. Authenticated tenant exists, but it assigns no outer identity. | No `AiRequests` admission in this service. It could be invoked repeatedly if a caller is added. | Can reach the primary provider; resulting snapshot is not propagated in its mapped legacy response. |
| `AIOrchestrator.ProcessAsync` → `IAIProvider.GenerateAsync` | Legacy orchestration service. | Legacy/non-dispatching for current HTTP traffic. | Its only controller returns 410. Repository search found no other production service caller; DI registration alone is not a dispatch. | The direct provider result is used by the legacy path, but it has no outer commercial identity and does not use the current aggregation contract. If reactivated, it requires an explicit telemetry boundary first. |
| `GroundedConciergeResponseGenerator.GenerateAsync` → `IConciergeLanguageModel.GenerateAsync` | Called within the primary orchestrator for eligible non-suggestion generated replies. | Internal sub-operation today. The factory selects `DevelopmentConciergeLanguageModel` whether its enabled option is true or false. | Runs within the parent request; no separate `AiRequests` admission, outer identity, or `ProviderCallId`. | Local deterministic response, no external paid call and no SDK usage. If generation succeeds it bypasses the primary provider; if it fails, the orchestrator may invoke the primary provider as fallback. It does not currently create two external paid calls. |

The current production provider selection is in
`backend/Extensions/ServiceCollectionExtensions.cs`. OpenAI is selected only
when configured; otherwise the development provider is selected.

## 3. Copilot replay lifecycle

`AdmitCopilotOperationAsync` validates an existing operation's company,
conversation, actor, and operation type. It does not validate a request
payload fingerprint, cache a result, or short-circuit based on operation
status. Callers then invoke the orchestrator. `CopilotOperation` stores
identity, type, status (`Pending`, `Completed`, `Failed`), and completion
time, but not the generated output.

| Replay state | Current observed control flow |
|---|---|
| A. Provider succeeded | Operation is marked `Completed`; replay admits the same operation without another `AiRequests` charge, then invokes orchestration and provider again. No stored result is returned. |
| B. Provider failure / deterministic fallback | Operation is marked `Failed` (fallback is treated as failure by these service paths); replay admits same ID and may invoke provider again. |
| C. Timeout / cancellation | Timeout generally becomes unavailable and fallback; caller cancellation propagates through service exception handling, which marks failed where possible. A later retry can invoke again. If cancellation also prevents status persistence, operation may remain pending. |
| D. Non-terminal operation | Existing `Pending` status does not cause rejection, waiting, or resume. Admission reuses the row and caller proceeds to orchestration. Concurrent requests can therefore both dispatch. |
| E. Admission succeeded, response/result persistence failed | The quota/operation admission persists before provider dispatch. If a later status update or outer response path fails, there is no stored result transactionally tied to the provider response. Retry can dispatch again; `AiRequests` remains idempotently charged once. |

**Same or new logical call recommendation:** For a repeated identical
Copilot operation ID and same semantic input, treat P1 and a possible P2 as
attempts of the **same logical provider call**; suppress P2 at the application
dispatch boundary. A deliberate new generation should use a new durable
operation ID. A reused operation ID with different semantic input should be
rejected as a conflict, not silently dispatched as another call. This aligns
with existing durable operation identity and quota idempotency, but requires
payload binding and an approved replay response contract.

Alternatives:

* Treating P1 and P2 as distinct logical calls preserves current endpoint
  behavior but makes one durable operation trigger unbounded repeated provider
  costs and creates ambiguity for quota, dedupe, and customer-facing results.
* Treating them as the same logical call without dispatch suppression merely
  deduplicates records after money may have been spent; this is not sufficient
  cost accounting.

## 4. Three identities

1. **Outer operation identity:** The business request that owns the work:
   `(OuterOperationType, OuterOperationId)`, e.g. persisted
   `ConversationMessage.Id` or durable `CopilotOperation.Id`. It can own zero,
   one, or multiple logical provider calls.
2. **Logical provider call identity:** Stable identity for one intended
   provider-level work unit under an outer operation, reused for identical
   request retries/replays and process restart. It must be available and
   atomically claimed before dispatch if replay suppression is required.
3. **Provider attempt identity:** One actual StayFlow-visible invocation
   attempt. It gets its own attempt ID and records outcome/usage (known or
   unknown). Multiple attempts can belong to one logical call.

Current `ProviderCallId` is attempt-scoped in practice, although its name could
be read as a logical call identifier. For durable storage, either rename the
concept to `ProviderAttemptId` or explicitly document its attempt semantics;
add a separate stable logical-call key/ID.

## 5. Multiple calls per outer operation and call role

An outer identity alone is not a valid uniqueness key: an operation may have
zero provider calls (validation, deterministic result, or feature-disabled
workspace), one call, or future multiple legitimate calls.

Current facts:

* The main orchestrator invokes at most one `IAIProvider` provider request in
  one orchestration pass. The grounded development path and primary path are
  alternatives: a successful grounded result skips the primary provider; a
  failed grounded result can fall through to it.
* Grounded language generation is local development-only today. It is not a
  paid provider attempt.
* A same-ID Copilot replay can invoke the main provider again.
* Retrieval diagnostics is a separate request with no outer ID and may invoke
  the provider on every call.
* Future provider fallback chains, intentional multi-stage generation, or
  retries may make multiple actual attempts/calls for one outer operation.

Minimum role recommendation: use one provider-neutral role for the existing
primary user-facing response-generation slot, not separate endpoint names for
suggestions/generated reply/workspace draft. Those are business operation
types, not distinct provider call roles. If diagnostics is admitted into
telemetry, give it a distinct diagnostics role/outer classification. If a
future pipeline has distinct paid model stages, add stable stage roles then.
Attempts/retries are represented separately and are not encoded by changing a
role.

An intentional second logical call within one role needs an explicit stable
ordinal/instance discriminator, not an outer-only unique constraint.

## 6. Grounding and retrieval diagnostics

### Grounded concierge

The grounded path executes within eligible generated-reply/guest operations
when configured in orchestration. `ConciergeLanguageModelProviderFactory`
always resolves `DevelopmentConciergeLanguageModel`, which is deterministic
and local. There is no current paid-call observability gap here. It has no
separate outer identity, `AiRequests` admission, provider attempt ID, token
usage, or provider/model fact because it is not currently external. If an
external implementation is registered later, it must be classified as part
of the parent operation or a distinct logical call before enabling it.

### Retrieval diagnostics

`CopilotService.GetRetrievalDiagnosticsAsync` checks authenticated tenant
context and the Host Copilot feature, then calls the primary orchestrator
without outer identity and without `AiRequests` admission. If OpenAI is
configured, this can be a paid provider call; the orchestrator creates a
runtime ID but does not create a token aggregation because both outer fields
are absent, and diagnostics response mapping does not expose usage.

Classification options:

* **Parent commercial operation:** Attribute it to an explicit
  `CopilotOperation` and apply `AiRequests` admission. This provides customer
  attribution but changes the diagnostics contract and quota behavior.
* **Internal StayFlow cost:** Keep it non-customer-metered, but classify and
  record it as internal tenant-scoped cost with an explicit internal call
  category; do not present it as customer usage. This needs budget/authorization
  policy even if there is no customer quota.
* **Separately metered:** Create a distinct durable commercial operation and
  quota/admission key. This is clearest if diagnostics is a paid product
  feature, but is a product/pricing decision.
* **Unresolved:** Current state. Do not claim complete commercial cost
  coverage.

Recommendation: no paid model call should be allowed from diagnostics until
the product owner chooses parent attribution, separate commercial admission,
or explicitly authorized internal cost classification. This is a Phase 2B.3
scope blocker.

## 7. ConversationAIExchange and legacy service

### ConversationAIExchangeService

* DI-registered as `IConversationAIExchangeService`.
* No backend caller found.
* It can invoke `IAIReplyOrchestrator`, which can dispatch to OpenAI depending
  on provider configuration.
* It assigns no outer identity and has no `AiRequests` admission.
* Its result mapping does not propagate the runtime usage snapshot.
* Classification remains `REGISTERED_BUT_CURRENTLY_UNUSED` /
  `DORMANT_UNCLASSIFIED`.

Do not assign `GuestMessage` merely because the method handles guest data. If
activated, first persist/resolve the actual guest-message identity, add
tenant-scoped admission, pass explicit outer identity, and include it in the
provider telemetry policy; alternatively remove/disable the path before
allowing a paid provider.

### Legacy AIOrchestrator

`POST /ai/orchestrate` remains 410. `IAIOrchestrator` is DI-registered, but
source search found no internal production caller of `ProcessAsync`. It is
currently non-dispatching for commercial traffic. If reactivated, it would
invoke `IAIProvider` without the current outer identity/aggregation path and
must be brought under telemetry/admission before enabling paid dispatch.

## 8. Retries and attempt visibility

* **Application layer:** Current AI orchestration has no explicit retry loop
  around `IAIProvider.GenerateAsync`. Its timeout handling and fallback are
  not retries by themselves. Separate HTTP APIs may have resilience settings,
  but the configured general `RetryCount` was not found wired into this AI
  invocation path.
* **OpenAI wrapper:** `OpenAIAIProvider` makes one call to
  `IOpenAIResponsesClient.CreateResponseAsync`; `OpenAIResponsesClient` makes
  one SDK `ResponsesClient.CreateResponseAsync` call. No wrapper retry loop is
  present.
* **SDK/HTTP transport:** The repository does not instrument or expose
  physical retries internal to the SDK/HTTP transport. SDK internals were not
  independently audited for exact retry policy in this source audit.

Therefore StayFlow can observe one application-level attempt per invocation,
not necessarily every provider/network attempt. Durable telemetry must not
claim physical-attempt precision unless the transport exposes each attempt.
An ambiguous timeout can mean the provider completed remotely even though
StayFlow observed no response or token usage.

## 9. Failure and crash semantics

Do not imply exactly-once execution across a database and an external provider.
Recommended meanings:

| State/event | Durable meaning |
|---|---|
| Prepared | A logical call has a durable identity/admission record; provider dispatch has not yet been authorized/started. Not an observed provider attempt. |
| Dispatch started | Application crossed the dispatch boundary. The provider may or may not have accepted it. |
| Success with known usage | Provider returned success and usage; preserve all provider-reported counts. |
| Success with unknown usage | Provider returned success but usage was absent; null is unknown, not zero. |
| Failure before usage | Provider/application failed without a usage observation; usage remains unknown. |
| Failure after usage observed | Preserve failure outcome and known usage independently; failed output can still have incurred tokens. |
| Caller cancelled | Record cancellation as the caller-visible outcome if the dispatch had begun. It does not prove the remote provider stopped. |
| Fallback used | Record fallback/business outcome separately from provider-attempt outcome. A deterministic fallback does not erase a failed/unknown/known-cost provider attempt. |
| Telemetry write failed after success | Keep the customer-facing result if policy requires, but mark telemetry delivery unresolved and retry persistence with the same attempt identity. Do not retry the provider merely to repair telemetry. |

Crash windows:

* **Record created, crash before dispatch:** Safe to regard as prepared/not
  dispatched only if the application has not crossed the recorded dispatch
  boundary.
* **Dispatch succeeds, crash before observation update:** The durable row can
  remain dispatch-started with usage unknown. It cannot prove whether the
  provider ran or what it charged.
* **Provider success, response persistence fails:** A stored business result
  and telemetry fact need separate retry policy. Do not redispatch solely to
  recover telemetry; cached business output must be stored in an approved
  result store if replay should return it.
* **Timeout but provider may have completed remotely:** Mark outcome/usage
  uncertain. Exactly-once external execution is not guaranteeable without
  provider idempotency support and a compatible provider contract.

## 10. Token usage authority

The current authoritative observation is `ResponseResult.Usage` from the
provider SDK, mapped directly by `OpenAIResponsesClient.MapTokenUsage` to
`AiTokenUsage`. Preserve provider-reported `InputTokenCount`,
`OutputTokenCount`, and `TotalTokenCount` as three source facts. Current code
does not enforce `TotalTokens == InputTokens + OutputTokens`; this audit does
not assume the provider contract guarantees equality in all accounting cases.
Do not recompute total and overwrite the provider-reported value.

Usage nullability remains meaningful:

* all three null: unknown usage;
* all three non-null, including `0/0/0`: known observation;
* partial null triples should be disallowed at durable write boundary unless
  a provider contract explicitly supplies partial metrics.

Negative token acceptance is not currently rejected by the `AiTokenUsage`
record and requires an explicit validation policy before persistence.

## 11. Provider and model identity

| Fact | Current source / time available | Durability and Phase 2C suitability |
|---|---|---|
| Provider selection | `AIProvider:Provider` selects OpenAI or Development in DI, known at dispatch. Result provider name is categorical (`OpenAI`, etc.). | Persist selected provider as a bounded category. Stable enough to identify provider family, not a pricing SKU alone. |
| Requested model | `OpenAIOptions.Model`, placed in the request before dispatch. | Record as requested/configured model. Do not treat it as immutable historical pricing identity without confirming alias pinning. |
| Response model | Adapter currently sets `ModelName = request.Model`; `OpenAIAIProvider` uses response model or configured fallback. | Current adapter effectively echoes requested model; actual provider-resolved model may not be known. Not sufficient alone for precise historical pricing. |
| Provider request/response ID | OpenAI `ResponseResult.Id`, available after successful response; failures often have none. | Optional diagnostic metadata only. Not a StayFlow idempotency/dedupe key and not available before dispatch. |
| Attempt/outer IDs | StayFlow-generated/current durable IDs. | Required correlation identities, not provider pricing metadata. |

Phase 2C pricing/cost is not ready to infer from current model metadata. Do not
add price, currency, cost, markup, or price-version fields in Phase 2B.3.

## 12. Tenant ownership and deletion

`CompanyId` is the canonical tenant identifier. Guest `ChatService` obtains
the company through validated conversation/tenant context and uses it for
admission and orchestration. Copilot services require authenticated tenant
context; existing `CopilotOperation` replay validation checks company,
conversation, actor, and operation type. Durable telemetry must obtain
`CompanyId` from trusted server context, never from an untrusted request
payload.

Current `ConversationMessage` and `CopilotOperation` records each carry
`CompanyId`. Their Company FKs use `Restrict`; ConversationMessage-to-
Conversation uses `Cascade`; CopilotOperation-to-Conversation uses `Restrict`.
Messages are soft-deleted. A future telemetry row needs an explicit retention
and hard-delete policy: if it has only opaque outer IDs without parent FKs,
it can survive parent deletion, but tenant ownership must remain trustworthy.
Cross-tenant GUID reuse must be rejected at the write/admission boundary.

## 13. Durable idempotency options

| Option | Replay / restart | Multiple calls | Dispatch/cost implications | Assessment |
|---|---|---|---|---|
| **A. Persist runtime `ProviderCallId` only** | Same in-process attempt can be updated; later request creates a new ID, so no replay dedupe. | Supports multiple rows naturally. | Deduplicates telemetry only for same runtime ID; does not suppress duplicate provider spend. | Necessary as an attempt identifier, insufficient as logical idempotency. |
| **B. Outer identity + call role** | Stable if outer ID and role remain stable across replay. | Requires role and possibly ordinal/instance key. | Can provide stable uniqueness, but callers must atomically claim it before dispatch. | Small durable key if payload binding and operation policy are defined. |
| **C. Stable LogicalProviderCallId** | Works only if ID is durably stored/retrieved before dispatch and reused across restart/replay. A random ID generated only in memory does not. | Multiple logical calls get separate stable IDs. | A pre-dispatch claim can suppress duplicate dispatch; an ID-only unique constraint after dispatch cannot. | Semantically clear, but needs an owning durable admission record. |
| **D. Outer identity + deterministic logical-call key** | Derive stable key from outer identity plus stable role/ordinal; no client prompt or random request ID. | Supports multiple roles/ordinals. | Unique pre-dispatch claim can gate duplicate application dispatch; attempts remain separate. | **Recommended smallest basis**, subject to replay/product decisions and atomic claim semantics. |

Recommendation: use outer identity plus a stable logical-call key (D) as the
logical identity, while retaining runtime `ProviderCallId` as an attempt ID.
Persist the claim before dispatch. Bind the logical identity to the canonical
request semantics (e.g. a non-sensitive payload fingerprint or immutable
operation inputs) so reuse with changed input is rejected. Do not store prompt
or response in telemetry. A separate approved result store is needed if a
completed replay must return the original output.

## 14. Dispatch suppression and concurrency

Telemetry dedupe and provider-dispatch suppression are different:

* A uniqueness constraint applied after the provider call can prevent a
  duplicate telemetry row, but cannot undo duplicate provider spend.
* If identical logical calls must dispatch once, durable admission must occur
  before provider invocation.
* Two concurrent requests must not both do “check then dispatch.” Use an
  atomic durable claim/insert or compare-and-set transition that elects one
  owner. A unique key alone can arbitrate claim ownership if creation occurs
  before external dispatch and losers observe the existing state.
* Do not hold a database transaction/row lock across a long network call as a
  simple exactly-once mechanism. Use a committed claim/lease/state transition;
  define owner expiry and stale-invoking recovery.
* A stale dispatch-started claim is ambiguous: the provider may have accepted
  the call. Automatically retrying risks duplicate cost; refusing retry risks
  an incomplete operation. Product/API policy must choose reconciliation,
  explicit retry/new operation, or provider-supported idempotency.

The achievable promise is **at-most-one application dispatch while a durable
claim is authoritative**, not exactly-once provider execution. Transport
retries internal to SDK/HTTP remain outside current visibility.

## 15. Recommended durable contract (semantic proposal only)

Each durable logical call has:

* trusted `CompanyId`;
* `OuterOperationType` + `OuterOperationId`, nullable together only for
  explicitly authorized internal/unclassified calls;
* stable `LogicalCallKey` (or equivalent ID), unique per outer operation and
  provider-neutral call role/ordinal;
* a stable semantic-input binding so same-key changed payload is rejected;
* a call role, initially primary response generation; diagnostics is a
  distinct classification if approved;
* one or more attempt records, each with a runtime `ProviderAttemptId`
  (current `ProviderCallId` semantics), start/completion observations,
  selected provider, requested model, optional provider request ID, categorical
  outcome/failure, and nullable usage triple;
* separate outer business result/fallback status; telemetry must not store
  prompts, generated output, exception text, stacks, or secrets.

Usage counts represent provider-reported values; all-null is unknown,
all-non-null is known, and zero is not unknown. Preserve provider-reported
total. A provider failure may retain known usage if returned. Attempt outcome
and deterministic fallback outcome are independent.

Suggested state meanings, not enum approval: Prepared (not dispatched),
DispatchStarted (provider may have received it), and terminal response
observed (Succeeded, Failed, Unavailable, or Cancelled). Persistence failure
after response is an observation-delivery problem, not a reason to repeat the
provider call.

## 16. Minimum Phase 2B.3 scope

**Include, after semantic approval:**

* durable pre-dispatch logical-call admission/claim;
* tenant-scoped provider-call/attempt facts;
* provider attempt identity and categorical lifecycle;
* nullable provider-reported usage and bounded provider/model/request metadata;
* replay/concurrency tests and Phase 2A regression tests;
* explicit policy for diagnostics and Copilot completed/in-progress/failed
  replays.

**Exclude from the initial slice:**

* provider exactly-once claims;
* hidden SDK/HTTP physical-attempt accounting unless instrumented;
* token pricing, cost, markup, billing, or customer token quota;
* broad analytics API, frontend, and UI;
* prompt/response content persistence.

**Defer:** retention/hard-delete policy and Phase 2C pricing identity. Resolve
retention before selecting tenant-FK deletion behavior; do not infer a cascade.

## 17. Migration readiness

**Are semantics sufficiently resolved to design a migration? NO.**

The recommended identity model is stated, but these decisions need approval
before translating it into constraints and lifecycle fields:

1. Copilot same-ID replay: response behavior for completed, failed, and
   in-progress operations; where original output lives; whether changed
   inputs under the same ID conflict.
2. Dispatch claim ownership, stale `DispatchStarted` recovery, and whether
   automatic retry is forbidden or requires a new attempt/operation.
3. Diagnostics classification: internal cost, separately metered commercial
   call, parent operation, or disabled for paid provider.
4. Dormant `ConversationAIExchangeService` and legacy service activation
   policy.
5. Retention/hard-delete behavior for tenant-owned telemetry.
6. Negative token validation and provider-reported total preservation policy.

Once approved, stable semantic fields include tenant ID; outer type/ID pair;
logical call key/role/ordinal; attempt ID; provider; requested model; optional
provider response ID; nullable all-or-none usage triple; bounded outcome and
failure category; state timestamps; and tenant-scoped uniqueness for logical
call admission and attempt identity. Exact FK, status constraints, indexes,
leases, and result storage belong in a separate schema-design step.

## 18. Repository hygiene and audit safety

Commit `7408874`'s seven zero-byte paths still exist:

`0`, `CopilotOperation`, `GuestMessage`, `conflicting`, `known:`, `same`,
`unknown:`.

They are repository hygiene debt from that commit, not Phase 2B.3 telemetry
scope. They were not changed or deleted.

At audit start, `main` and `origin/main` both pointed to
`6deb4c09b80f7354f4f5b5555d41a8d47131e4c6`. The tracked tree was clean. The
pre-existing untracked file
`SPRINT20_AI_TOKEN_PERSISTENCE_AUDIT_PHASE_2B_3.md` was left unchanged. No
source, test, migration, or other file was modified by this audit before
creating this report.

**Audit report:** `SPRINT20_AI_TOKEN_PERSISTENCE_SEMANTICS_AUDIT.md`  
**Tracked:** No (new untracked report)  
**Staged / committed / pushed:** No / No / No  
**Database / PostgreSQL / OpenAI contacted:** No / No / No  
**Migration generated / applied:** No / No

## Final assessment

Current Phase 2B.2 telemetry is runtime-only and correctly separates outer
operation identity from observed provider invocation identity. Durable
Phase 2B.3 should add a stable logical-call claim and separate attempt facts;
it must not mistake a newly generated runtime ID or an outer operation alone
for a complete idempotency key.

The recommended replay policy is to suppress redispatch for the same
Copilot operation and unchanged semantic input, with a new operation ID for
deliberate regeneration. Current source does not implement that policy and
does not store a replay result. Diagnostics also has unresolved paid-cost
classification. Approve these behaviors and stale-claim recovery before
schema design. Do not promise exactly-once provider execution.

**NOT READY FOR PHASE 2B.3 SCHEMA — approve Copilot replay/result and stale-dispatch recovery semantics, and classify paid retrieval diagnostics before fixing durable uniqueness and admission rules.**
