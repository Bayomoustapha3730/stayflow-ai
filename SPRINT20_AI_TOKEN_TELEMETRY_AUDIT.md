# PHASE 2B AI TOKEN TELEMETRY AUDIT

Repository: `/workspace`
Audit mode: read-only
Date: 2026-09-23

## BASELINE

- Branch: `main`
- HEAD: `bb9be9c3989a2bbe26bacc07cdef60c882a5cfb2`
- Upstream: `bb9be9c3989a2bbe26bacc07cdef60c882a5cfb2`
- Clean tracked baseline: YES. `git status -sb` showed `main...origin/main`; HEAD and upstream matched.
- Existing untracked reports were identified separately and left untouched: `SPRINT20_AI_USAGE_METERING_AUDIT.md`, `SPRINT20_RESERVATION_LIFECYCLE_BADGE_REPORT.md`, `SPRINT20_RESERVATION_QUOTA_AUDIT.md`, `SPRINT20_RESOURCE_CAPACITY_REPORT.md`.
- This report is the only file created by this audit.

## PROVIDER ARCHITECTURE

### Interfaces and implementations

- `backend/Services/IAIProvider.cs`: `IAIProvider.GenerateAsync(AIProviderRequest, CancellationToken)` returns `AIProviderResult`.
- `backend/Services/OpenAIAIProvider.cs`: production `OpenAIAIProvider` implementation.
- `backend/Services/DevelopmentAIProvider.cs`: deterministic provider; returns fixed/guided text, synthetic request ID, provider name `Development`, model `stayflow-development-deterministic`, and duration.
- `backend/Services/OpenAIProviderContracts.cs`: local `IOpenAIResponsesClient`, `OpenAIProviderRequest`, `OpenAIProviderMessage`, `OpenAIProviderResponse`, and `OpenAIProviderException` boundary.
- `backend/Services/OpenAIResponsesClient.cs`: OpenAI SDK adapter.
- `backend/Services/AI/Orchestration/IConciergeLanguageModel.cs`: separate concierge language-model abstraction returning `ConciergeLanguageModelResult`.
- `backend/Services/AI/Orchestration/DevelopmentConciergeLanguageModel.cs`: current factory-selected concierge model; deterministic only in this checkout.
- `backend/Services/AI/Orchestration/GroundedConciergeResponseGenerator.cs`: calls the concierge abstraction, then validates the generated result.
- `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs`: current shared orchestration path for guest generated replies, host suggestions, host generated replies, and workspace drafts.
- `backend/Services/AIOrchestrator.cs`: legacy orchestration path; its endpoint is now HTTP 410 and should not be treated as an active commercial path.
- `backend/Services/ConversationAIExchangeService.cs`: direct authenticated conversation exchange path; calls `AIReplyOrchestrator`.
- `backend/Services/ChatService.cs`: guest admission and commercial guest chat path.
- `backend/Services/CopilotService.cs`: host suggestion and generated-reply admission through `CopilotOperation`.
- `backend/Services/HostCopilot/HostCopilotWorkspaceService.cs`: workspace listing is deterministic; draft generation can call `AIReplyOrchestrator` when `HostCopilot:EnableLlmWording` is enabled.

### Call graph: Guest Chat

`ChatController.SendMessage` or WhatsApp inbound processing
-> `ChatService.SendGuestMessageAsync`
-> feature check and `ConsumeQuotaAsync(AiRequests, 1, ai-request:guest-message:{ConversationMessage.Id})`
-> `AIReplyOrchestrator.OrchestrateAsync(FutureGuestReply)`
-> optional `IGroundedConciergeResponseGenerator.GenerateAsync` / `IConciergeLanguageModel.GenerateAsync`
-> otherwise `IAIProvider.GenerateAsync`
-> `OpenAIAIProvider.GenerateAsync` or `DevelopmentAIProvider.GenerateAsync`
-> `IOpenAIResponsesClient.CreateResponseAsync`
-> `ResponsesClient.CreateResponseAsync(CreateResponseOptions)` (Responses API).

Concierge action handling may return a deterministic action response before AI quota admission and provider orchestration.

### Call graph: Copilot suggestions

`CopilotController` endpoint
-> `CopilotService.GetSuggestedRepliesAsync`
-> `AdmitCopilotOperationAsync` with `CopilotOperation.Id` and `AiRequests +1`
-> `AIReplyOrchestrator.OrchestrateAsync(SuggestedHostReplies)`
-> grounded concierge abstraction when enabled, otherwise `IAIProvider`
-> OpenAI Responses adapter or development provider
-> orchestration validation/fallback
-> mark `CopilotOperation` completed/failed.

### Call graph: Generated reply

`CopilotController` endpoint
-> `CopilotService.SuggestHostReplyAsync`
-> `AdmitCopilotOperationAsync` with `CopilotOperation.Id` and `AiRequests +1`
-> `AIReplyOrchestrator.OrchestrateAsync(GeneratedHostReply)`
-> grounded concierge abstraction when enabled, otherwise `IAIProvider`
-> provider adapter/provider implementation
-> validation/fallback
-> mark operation completed/failed.

### Call graph: Workspace draft

`HostCopilotController.GenerateDraft`
-> `HostCopilotWorkspaceService.GenerateDraftAsync`
-> `AdmitCopilotOperationAsync` with `CopilotOperation.Id` and `AiRequests +1`
-> if `EnableLlmWording`: `AIReplyOrchestrator.OrchestrateAsync(GeneratedHostReply)`
-> grounded concierge abstraction when enabled, otherwise `IAIProvider`
-> provider implementation/SDK
-> validation; deterministic draft if orchestration fallback or empty output
-> mark operation completed/failed.

`HostCopilotWorkspaceService.GetWorkspaceAsync` only reads context and computes safety, priority, SLA, recommendations, and timeline. It does not invoke an AI provider.

## OPENAI SDK

- Package: `OpenAI`
- Version: `2.12.0` from `backend/backend.csproj` and local `/root/.nuget/packages/openai/2.12.0` metadata.
- Namespace used by StayFlow: `OpenAI.Responses`.
- Client type: `OpenAI.Responses.ResponsesClient`.
- Request type used by StayFlow: `OpenAI.Responses.CreateResponseOptions`.
- Response type used by StayFlow: `OpenAI.Responses.ResponseResult`.
- API: Responses API. The production adapter does not use Chat Completions or Assistants.
- SDK call: `responsesClient.CreateResponseAsync(options, cancellationToken)` in `backend/Services/OpenAIResponsesClient.cs`.
- The request maps model, max output token count, and input items. StayFlow reads `GetOutputText()`, `Id`, and `Status`; it assigns the configured/requested model to `ModelName` rather than reading a returned model member.

## TOKEN METADATA

The locally installed assembly contains the following public Responses usage type names and usage-detail type names in metadata:

| Field/metadata | Status from local evidence | Location/meaning |
|---|---|---|
| `ResponseResult.Usage` | AVAILABLE in SDK metadata; NOT READ by StayFlow | Response object usage member; the adapter currently discards it. |
| `ResponseTokenUsage.InputTokenCount` | AVAILABLE in SDK metadata | `OpenAI.Responses.ResponseTokenUsage`; input token count. Exact nullability was not established without compiling/reflection against the assembly. |
| `ResponseTokenUsage.OutputTokenCount` | AVAILABLE in SDK metadata | `OpenAI.Responses.ResponseTokenUsage`; output token count. Exact nullability was not established. |
| `ResponseTokenUsage.TotalTokenCount` | AVAILABLE in SDK metadata | `OpenAI.Responses.ResponseTokenUsage`; total token count. Exact nullability was not established. |
| `ResponseTokenUsage.InputTokenDetails` / `ResponseInputTokenUsageDetails` | AVAILABLE in SDK metadata | Input usage detail object. Exact public member set/semantics not established from the installed binary strings alone. |
| Cached input token detail | POSSIBLY AVAILABLE; exact member name/shape requires a local compiler/reflection probe | Binary metadata includes cached-token detail names, but this audit did not invent a public property name. |
| Reasoning token detail | POSSIBLY AVAILABLE; exact member name/shape requires a local compiler/reflection probe | Binary metadata includes reasoning-token detail names, but exact public property name/type was not established. |
| Audio/tool-specific usage | NOT ESTABLISHED for this non-streaming text response path | The assembly contains usage types for other APIs and modalities; no source evidence shows this path requests or receives them. |

Therefore, the provider contract currently exposes **no token metadata**, despite the installed Responses SDK exposing a usage object. Usage is lost in `OpenAIResponsesClient` when constructing `OpenAIProviderResponse`, before `OpenAIAIProvider` constructs `AIProviderResult`.

## MODEL AND PROVIDER METADATA

| Metadata | Classification | Evidence |
|---|---|---|
| Provider name | RETURNED/DERIVABLE LOCALLY | `OpenAIAIProvider` sets `OpenAI`; development provider sets `Development`. |
| Requested model | REQUESTED | `OpenAIOptions.Model` is sent as `CreateResponseOptions.Model`; `AIProviderResult.ModelName` uses it. |
| Actual returned model | NOT AVAILABLE THROUGH CURRENT CONTRACT | Adapter sets `ModelName = request.Model`; it does not read a returned model field. |
| Provider response/request ID | RETURNED BY PROVIDER | `ResponseResult.Id` is copied to `OpenAIProviderResponse.RequestId`, then `AIProviderResult.RequestId`. Some higher orchestration mapping later drops it. |
| Service tier | NOT AVAILABLE THROUGH CURRENT CONTRACT | No local adapter field or mapping. |
| Finish/status reason | PARTIAL | Adapter reads `ResponseResult.Status` only to derive `IsIncomplete` and `IsRefusal`; no raw status/finish field crosses the boundary. |
| Latency | DERIVABLE LOCALLY | `Stopwatch` around the adapter/provider call becomes `DurationMs`; this is application elapsed time, not provider-reported latency. |
| Token usage | AVAILABLE IN SDK, NOT AVAILABLE INTERNALLY | `ResponseResult.Usage` is not mapped. |

## CURRENT ABSTRACTION

### Current result fields

`AIProviderResult` contains: `Outcome`, `ResponseText`, `ProviderName`, `ModelName`, `RequestId`, `DurationMs`, and `FailureCategory`.

The separate `AIProviderMetadata` contains: `ProviderName`, `ModelName`, `RequestId`, and nullable `DurationMs`.

`ConciergeLanguageModelResult` contains output, success, provider, model, request ID, duration, timeout/fallback flags, warning/failure/validation fields, source IDs, safety classification, and `RawProviderResponse`; it also has no token fields. The current concierge factory selects the development implementation regardless of the enabled branch.

- Token usage survives abstraction: **NO**.
- Exact loss boundary: SDK `ResponseResult.Usage` is dropped in `OpenAIResponsesClient.CreateResponseAsync` while creating `OpenAIProviderResponse`; no usage member exists on `OpenAIProviderResponse`, `AIProviderResult`, `AIOrchestrationResult`, or `AIProviderMetadata`.

## COMMERCIAL OPERATIONS

Provider-call counts below are based on source-visible control flow, not live-provider behavior.

### Guest Chat

- Minimum provider calls: 0. Deterministic concierge actions, deterministic knowledge/clarification responses, missing context, or pre-provider escalation can complete without a provider call.
- Normal provider path: 1 call through either grounded concierge generation or `IAIProvider`, not both in the same orchestration invocation. If grounded generation succeeds, normal provider invocation is skipped.
- Additional calls: no source-visible internal generation loop, repair call, tool round, or application retry in `AIReplyOrchestrator`.
- Fallback: deterministic fallback can follow a failed, unavailable, invalid, unsafe, or insufficient provider result. It does not invoke another provider in the visible path.
- Maximum: source-visible maximum is 1 provider call per orchestration invocation; outer operation retries/re-entry are not globally bounded by this code path and should be treated as UNKNOWN.

### Copilot suggestions

- Minimum provider calls: 0, because orchestration can resolve deterministically or apply fallback without invoking a provider.
- Normal path: at most 1 source-visible provider call.
- Fallback: deterministic fallback after provider failure/validation/safety/context conditions; no second paid provider is visible.
- `CopilotOperation` admission is once per operation ID and charged as one `AiRequests` unit; token aggregation must not assume one token-bearing call from that fact alone.

### Generated reply

- Minimum provider calls: 0.
- Normal path: at most 1 source-visible provider call through grounded concierge or `IAIProvider`.
- Fallback: deterministic fallback after the provider result is available or after no provider is invoked. No repair/secondary provider call is visible.

### Workspace draft

- Minimum provider calls: 0. `EnableLlmWording=false` is deterministic; orchestration can also produce fallback.
- Normal path: at most 1 source-visible provider call when LLM wording is enabled.
- Fallback: deterministic draft after orchestration fallback/empty output; no second provider call is visible.
- Workspace listing itself: 0 provider calls.

## FALLBACK BEHAVIOR

- A failed or timed-out provider call can be followed by a deterministic fallback result within the same admitted `AiRequests` operation.
- A provider exception can therefore coexist with a successful outer response/fallback.
- The deterministic fallback itself does not invoke another paid provider in source-visible code.
- Multiple provider calls contributing to one outer request are not produced by the current orchestration loop, but repeated client requests/re-entry are not represented by a common outer identity for Guest Chat. Treat cross-request aggregation as an explicit future design requirement.

## RETRIES

- Application provider retry: **NO source-visible retry loop** in `OpenAIAIProvider`, `OpenAIResponsesClient`, or `AIReplyOrchestrator`. Timeouts and exceptions become result states or fallback.
- HTTP retry: OpenAI uses `ResponsesClient` directly, not the named `HttpClient` retry path used by WhatsApp. No application OpenAI HTTP retry policy is configured in source.
- SDK retry: **UNKNOWN** from source. The installed SDK binary metadata does not prove whether `ResponsesClient` internally retries. Establish this with package source/documentation or a controlled fake/transport test before billing semantics rely on one physical request per logical call.
- Quota admission retry: `QuotaAdmissionRetryPolicy` retries serializable/unique/deadlock quota races up to three times. This retries admission/database work, not the provider call, and must not be counted as AI token generation.

## STREAMING

**CURRENT AI EXECUTION IS NON-STREAMING** for the production text path shown by source: `ResponsesClient.CreateResponseAsync` returns a complete `ResponseResult`, and no production AI code uses a streaming Responses method or streaming result type. The SDK contains streaming types, but source does not use them. Final usage delivery for a streaming future path is therefore NOT AVAILABLE from current application code.

## CURRENT AITOKENS

- Plan limits: `UsageMetric.AiTokens` exists and maps to entitlement key `Quota.AiTokens`; seed data assigns a plan limit of 200000. Plan/quota snapshots can expose it.
- Production usage writes: **NO** source reference calls `ConsumeQuotaAsync(UsageMetric.AiTokens, ...)`. Existing production writes shown in the commercial paths are `AiRequests`.
- Hard enforcement: **NO** for `AiTokens` in Phase 2A. `AiTokens` remains an unused quota metric, not an admission guard.
- Billing/admin display: plan entitlement infrastructure can represent the metric; `PlatformAdminController` currently reports `AiRequests` and `ApiRequests` for tenant 30-day usage, not `AiTokens`. A DTO named `AiTokensLast30Days` exists, but no production write path was found.

## USAGE LEDGER

- Arbitrary quantity supported: **YES**, technically. `UsageRecord.QuantityUsed` and `UsageOperation.Quantity` are `long`; `ConsumeQuotaAsync` accepts `long quantity` and adds it with checked arithmetic.
- Idempotent quantity: **YES for one logical key and period**, through unique `(CompanyId, Metric, PeriodStartUtc, IdempotencyKey)` and replay lookup. The idempotency key is caller-supplied.
- Aggregate operation possible: **YES without schema change** if one `UsageOperation` represents the aggregate token total and uses `AiTokens` only as a recording operation. However, the current API also enforces entitlement availability and limit, so it is semantically a quota/admission API, not telemetry-only recording.
- Per-call detail possible: **NO** with current schema. UsageOperation stores metric, period, idempotency key, period, and quantity, but no provider, model, provider request ID, call kind, or outer operation ID beyond whatever is encoded into the key.
- Detail loss: **YES**. Aggregating multiple calls into one quantity loses per-provider-call attribution; multiple ledger rows could preserve only key/quantity, not structured metadata.
- Billing-period behavior: the service loads the current subscription at consumption/write time and uses `subscription.CurrentPeriodStartUtc` and `CurrentPeriodEndUtc`. It does not bind usage to provider admission time or an explicit period supplied by the caller. An operation admitted before a boundary but recorded after it would be assigned to the period observed at the later write unless a future telemetry design captures the admission period explicitly.

## FAILURE TELEMETRY

- Successful response: SDK usage is likely available on `ResponseResult.Usage`, but current adapter discards it; successful token telemetry is not available to application code.
- Provider error: current exception mapping returns category, requested model, duration, and usually no request ID. Whether a failed provider response can carry usage is UNKNOWN from current source because exceptions do not expose a mapped response usage object and no live provider request is allowed.
- Timeout/cancellation: current code returns unavailable with no request ID and no usage. The provider may have consumed tokens before timeout, but that cannot be established from this source-only audit; telemetry would be unavailable in the current contract.
- Application error after provider response: possible. The provider response is returned before validation, persistence, message write, or later usage persistence in any future design; an exception in those later stages could leave a successful provider call unrecorded unless telemetry persistence is independently handled.
- Known from source: current application loses response usage and does not persist token usage.
- Unknown without live/provider-specific evidence: usage on failed/timeout responses, SDK retry count, and whether provider-side consumption occurred after transport cancellation.

## TELEMETRY VS QUOTA

Future token capture must be separate from `ConsumeQuotaAsync`/`AdmitCopilotOperationAsync` semantics. Calling `ConsumeQuotaAsync(AiTokens, N, ...)` would currently check entitlement enablement, current quota, and throw `QuotaExceededException`; that would accidentally introduce hard token admission/enforcement. Phase 2A must remain `AiRequests` enforced and `AiTokens` telemetry-only.

## PROPOSED TELEMETRY CONTRACT

Conceptual only; not implemented:

| Field | Status | Basis |
|---|---|---|
| `InputTokenCount` | AVAILABLE NOW at SDK response boundary; not exposed internally | Local SDK metadata names `ResponseTokenUsage.InputTokenCount`. |
| `OutputTokenCount` | AVAILABLE NOW at SDK response boundary; not exposed internally | Local SDK metadata names `ResponseTokenUsage.OutputTokenCount`. |
| `TotalTokenCount` | AVAILABLE NOW at SDK response boundary; not exposed internally | Local SDK metadata names `ResponseTokenUsage.TotalTokenCount`. |
| Cached input tokens | OPTIONAL / exact member shape not yet confirmed | SDK metadata contains cached-token usage detail names. Confirm with compiler/reflection before contract naming. |
| Reasoning tokens | OPTIONAL / exact member shape not yet confirmed | SDK metadata contains reasoning-token usage detail names. Confirm before contract naming. |
| Provider | AVAILABLE NOW | Existing provider result. |
| Requested model | AVAILABLE NOW | OpenAI options/request. |
| Returned model | NOT AVAILABLE currently | Adapter currently substitutes requested model. |
| Provider request ID | AVAILABLE NOW on successful Responses result | `ResponseResult.Id` is already captured by adapter, though later mappings drop it. |
| Finish/status | PARTIAL | Incomplete/refusal are derived; raw status is not exposed. |
| Duration | DERIVABLE LOCALLY | Existing stopwatch measurement. |
| Outer operation ID | AVAILABLE for host Copilot; Guest Chat needs a durable operation identity | `CopilotOperation.Id` exists; Guest Chat currently keys admission to `ConversationMessage.Id`. |
| Provider-call ID | NOT AVAILABLE as a durable application identity | Provider request ID is not equivalent to a local call entity and is absent on some failures. |

The smallest safe first contract is counts plus provider, requested model, provider request ID when present, outcome/status, and duration. Add cached/reasoning categories only after exact SDK member inspection and provider behavior tests.

## AGGREGATION

Recommended boundary: **commercial-operation-aware orchestration/service boundary**, immediately after `AIReplyOrchestrator` returns and before the outer service persists the user-visible result, with a separate telemetry-only recorder. The provider adapter is the first place usage exists, but it does not know the outer `CopilotOperation.Id` and cannot aggregate fallback/outer-operation semantics. Admission is too early to know actual calls and risks quota coupling. The commercial service knows the durable operation identity and billing boundary but needs an orchestration result containing aggregate usage.

Caveat: for detailed future analytics, the orchestrator/provider-call boundary should emit a usage event per provider invocation, while the commercial service aggregates those events under the outer identity. Do not infer token totals from one `AiRequests` admission.

- Guest outer identity: `ConversationMessage.Id` is the current durable admission identity and is sufficient to associate one aggregate telemetry record if the future design defines the same message as the operation. It does not identify repeated/retried generation attempts independently.
- Host outer identity: `CopilotOperation.Id` is suitable for idempotent aggregate association and covers suggestions, generated replies, and workspace drafts.
- Provider-call identity: **YES, needed for detailed provider/model/call/fallback/cost analytics**. `ResponseResult.Id` is useful when present, but a local provider-call ID is needed for failures without a provider response, retries, and deterministic correlation.

## PERSISTENCE OPTIONS

### Option A: existing UsageRecord/UsageOperation only

- Migration: NO.
- Aggregate totals: YES; `long Quantity`/`QuantityUsed` support token totals.
- Per-call provider/model attribution: NO, except lossy encoding in idempotency keys.
- Cost analytics: NO.
- Fallback/multiple calls: aggregate possible, detailed attribution lost.
- Idempotency: YES per metric/company/period/key.
- Complexity/risk: lowest schema risk, highest semantic risk because current API is quota-enforcing and period-selected at write time.

### Option B: extend existing usage ledger with metadata

- Migration: YES.
- Aggregate totals: YES.
- Per-call provider/model attribution: YES if metadata columns/relations are designed correctly.
- Cost analytics: possible, but likely needs token categories, pricing version, currency, and timestamps.
- Fallback/multiple calls: YES with explicit outer operation and provider-call fields.
- Idempotency: YES with revised unique keys/constraints.
- Complexity/risk: medium; risks mixing quota admission records with analytics telemetry and creating a broad schema contract.

### Option C: dedicated AI provider-call/usage telemetry persistence

- Migration: YES.
- Aggregate totals: YES, with a derived or separately maintained aggregate.
- Per-call provider/model attribution: YES.
- Cost analytics: YES, with pricing/version fields designed independently.
- Fallback/multiple calls: YES; natural fit for one row/event per physical provider call plus outer operation linkage.
- Idempotency: YES with outer-operation and provider-call uniqueness rules.
- Complexity/risk: highest implementation and operational complexity, but clearest separation between telemetry and enforced quota.

## MIGRATION DECISION

- Required for the narrowest aggregate-only telemetry: **NO**, existing numeric ledger columns can represent `AiTokens = N`.
- Required for a safe, telemetry-only, per-provider-call contract with model/provider/cost attribution: **YES**, or **NOT YET DETERMINABLE** until the provider contract and aggregation/idempotency rules are finalized.
- Recommended Phase 2B decision: **NOT YET DETERMINABLE for full telemetry design**. First extend/test the provider result contract in a future implementation phase; do not generate a migration in this audit. Option A is technically representable but is not safe as-is because its API enforces quota and cannot retain call metadata.

## COST ANALYTICS

- Current model pricing tables: NOT FOUND.
- Provider pricing configuration: NOT FOUND.
- Cost calculation logic: NOT FOUND.
- Currency fields for AI usage: NOT FOUND.
- Token-category pricing: NOT FOUND.
- Historical pricing/versioning: NOT FOUND.
- Phase 2C requires new pricing data/design, including provider/model/category rates, currency, effective dates/versioning, rounding, and treatment of unknown/failed usage. No public internet prices were used.

## PRIVACY

- Content persistence required: NO. Counts and operational metadata are sufficient for useful telemetry; prompt/response/customer message content should not be persisted for token accounting.
- PII required: NO. Use tenant and durable operation IDs already present, provider/model/request IDs, counts, status, and timestamps. Avoid copying prompt or response text into telemetry.
- Existing logs do include operational counts/lengths and some AI previews in the legacy path; future token telemetry should not add content fields.

## OBSERVABILITY

- Current: structured `ILogger` logs include correlation ID, provider/model, duration, outcome, failure category, prompt/message counts, and orchestration stages. Correlation ID middleware and `ICurrentTenantContext.CorrelationId` are available. No dedicated AI token `Meter`, counter, histogram, or tracing span was found.
- Future opportunity: emit token counts and call outcome to logs/metrics/traces at the provider boundary, tagged by provider/model/status and correlation/outer operation ID, while keeping durable billing/analytics persistence separate. Observability must not become the quota authority.

## TESTABILITY

- Synthetic usage possible: development providers already return deterministic synthetic request IDs/durations and can be extended in a future phase to return synthetic usage without a real provider.
- Provider abstraction change required: YES. `AIProviderResult`, `OpenAIProviderResponse`, and likely `ConciergeLanguageModelResult` need a usage-bearing contract if both provider paths are to be measured.
- Recommended focused tests, without a real provider:
  1. fake `IOpenAIResponsesClient` returns a synthetic response with usage; assert adapter maps all supported fields.
  2. one provider call maps one aggregate operation.
  3. grounded concierge success versus normal provider path does not double count.
  4. provider failure followed by deterministic fallback preserves one outer `AiRequests` admission and records known/unknown token usage separately.
  5. multiple provider-call events aggregate exactly once under one `CopilotOperation.Id`.
  6. duplicate telemetry delivery is idempotent.
  7. timeout/cancellation and exception-after-response behavior are represented as unknown usage rather than guessed zero.
  8. Guest `ConversationMessage.Id` and host `CopilotOperation.Id` tenant isolation is enforced.

## PRE-DEPLOYMENT CONCURRENCY

- Status: unchanged and deferred.
- Deferred matrix unchanged:
  1. WhatsApp distinct-operation final-unit race
  2. WhatsApp same-operation concurrent replay
  3. WhatsApp first UsageRecord creation race
  4. WhatsApp serialization/deadlock retry
  5. Membership final-seat race
  6. Property final-slot race
  7. Reservation distinct-operation final-unit race
  8. Reservation same-operation concurrent replay
  9. Reservation serialization/deadlock retry
  10. AiRequests distinct-operation final-unit race
  11. AiRequests same-operation concurrent replay
  12. AiRequests serialization/deadlock retry
  13. CopilotOperation same-ID concurrent creation
  14. AI tenant isolation under concurrent writes

## FILES

- Audit report: `SPRINT20_AI_TOKEN_TELEMETRY_AUDIT.md`
- Tracked source modified: YES, provider telemetry contract and OpenAI adapter/provider only
- Tests modified: YES, focused provider telemetry coverage only
- Migration modified: NO
- Frontend modified: NO
- Staged: NO
- Committed: NO
- Pushed: NO

## SAFETY

- `DefaultConnection` used: NO
- Persistent PostgreSQL contacted: NO
- Persistent DB modified: NO
- AI provider contacted: NO
- Migration generated: NO
- Migration applied: NO
- Secrets/PII printed: NO

## PHASE 2B.1 IMPLEMENTATION RESULT

Provider-level propagation was implemented without persistence:

- SDK package/version: `OpenAI` `2.12.0`.
- SDK response: `OpenAI.Responses.ResponseResult`.
- SDK usage: `ResponseResult.Usage`, with verified core properties `InputTokenCount`, `OutputTokenCount`, and `TotalTokenCount`.
- StayFlow contract: `backend/DTOs/AIProvider/AiTokenUsage.cs`, immutable `AiTokenUsage(long InputTokens, long OutputTokens, long TotalTokens)`.
- Provider result: additive nullable `AIProviderResult.TokenUsage`.
- OpenAI mapping: `OpenAIResponsesClient` maps provider-reported counts directly; no estimation or tokenization.
- Unknown usage: `null`; known zero remains a non-null `AiTokenUsage(0, 0, 0)`.
- Deterministic production providers remain usage-unknown; tests can return synthetic values.
- Optional cached/reasoning/audio categories were not added because exact safe Responses contract members were not established from the local package documentation.
- Fallback aggregation remains deferred: a provider attempt's usage is not yet aggregated into an outer operation result.

Direct SDK-boundary coverage was added:

- Test: `OpenAIResponsesClient_MapsActualSdkResponseUsage`.
- Actual SDK objects: `ResponseResult` containing `ResponseTokenUsage`.
- Controlled values: input `137`, output `41`, total `178`.
- Production path exercised: `ResponseResult.Usage` -> `OpenAIResponsesClient.MapTokenUsage` -> `AiTokenUsage`.
- Absent SDK usage is asserted as `null`.
- No real OpenAI request was made.

Validation:

- Direct SDK mapping test: 1 passed, 0 failed, 0 skipped.
- Focused OpenAI provider tests: 24 passed, 0 failed, 0 skipped.
- Full backend tests: 1,104 passed, 0 failed, 0 skipped.
- Frontend tests: 43 files, 262 tests passed.
- Frontend typecheck: passed.
- Frontend production build: passed.
- EF pending model changes: none.
- `git diff --check`: passed.
- No persistence, migration, frontend, pricing, cost, or `AiTokens` quota changes.

Phase 2B.1 changed tracked files only within the allowed provider contract, OpenAI adapter/provider, and focused test scope. The new contract is transport-only; telemetry persistence, aggregation, provider-call identity, observability, and cost analytics remain deferred. The temporary SDK-inspection solution entry was removed before closure.

PHASE 2B.1 COMPLETE — READY FOR PROVIDER CONTRACT PRE-COMMIT AUDIT
