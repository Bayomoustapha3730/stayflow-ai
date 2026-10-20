# SPRINT 20 — AI TOKEN AGGREGATION & PROVIDER-CALL IDENTITY AUDIT

## Baseline

Verified on the working tree:

- Branch: main
- HEAD: 9d8bbee93cfff0818654aaf800aa8711a2dd394a
- Upstream: 9d8bbee93cfff0818654aaf800aa8711a2dd394a
- Tracked baseline: clean
- Existing untracked reports: allowed

This audit is read-only and does not modify production code, tests, frontend, migrations, DB state, or external providers.

---

## 1. Executive conclusion

The current backend architecture does not yet implement the durable aggregation model described in the Phase 2B.2 design target.

Evidence from the current source:

- Guest commercial outer identity is the persisted guest message ID, created as `ConversationMessage.Id` before the provider call.
- Host commercial outer identity is the persisted Copilot operation ID, created as `CopilotOperation.Id` before the provider call.
- The provider abstraction (`IAIProvider.GenerateAsync`) accepts a `CorrelationId` but no outer-operation identity or provider-call identity.
- `AIProviderResult.TokenUsage` exists and is populated from OpenAI `ResponseTokenUsage`, but the path only preserves the final result object; there is no aggregation or per-attempt capture across multiple provider calls.
- OpenAI provider failures can leave token usage unknown or absent. The code distinguishes `Outcome` and `FailureCategory`, but not a separate “attempt happened, usage unknown” state from “no provider call made.”
- The application intentionally uses quota admission separate from token telemetry: guest quota is charged using `UsageMetric.AiRequests` at the outer message boundary, while token usage is not part of current quota enforcement.

The defensible Phase 2B.2 target is therefore:

- keep `AiRequests` billing and quota admission separate from telemetry aggregation;
- create a StayFlow-owned provider-call identity at the point of each provider invocation attempt;
- correlate each provider attempt to the outer operation (`ConversationMessage.Id` or `CopilotOperation.Id`);
- aggregate only provider-reported usage from distinct provider attempts, preserving known/unknown state rather than assuming a single final result is the whole truth.

---

## 2. Commercial AI entry points and exact call chains

### A. Guest Chat

Controller: `ChatController.SendMessage`

Call chain:

- `backend/Controllers/ChatController.cs` -> `ChatController.SendMessage`
- `backend/Services/ChatService.cs` -> `SendGuestMessageAsync`
- `backend/Services/ConversationService.cs` -> `AddGuestMessageAsync`
- `backend/Services/ChatService.cs` -> `aiReplyOrchestrator.OrchestrateAsync(...)`
- `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs` -> `OrchestrateAsync`
- `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs` -> `InvokeProviderWithTimeoutAsync`
- `backend/Services/IAIProvider.cs` -> `GenerateAsync`
- `backend/Services/OpenAIAIProvider.cs` or `backend/Services/DevelopmentAIProvider.cs`
- `backend/Services/OpenAIResponsesClient.cs` -> `CreateResponseAsync` (OpenAI path)

Commercial admission location:

- `ChatService.SendGuestMessageAsync` calls `subscriptionEntitlementService.ConsumeQuotaAsync(companyId, UsageMetric.AiRequests, 1, $"ai-request:guest-message:{guestMessage.Data.Id:N}")`.

Outer operation identity:

- Guest message ID: `guestMessage.Data.Id` created by `ConversationService.AddGuestMessageAsync` as `new ConversationMessage { Id = Guid.NewGuid() ... }` before orchestration.

AI orchestrator/service invoked:

- `AIReplyOrchestrator.OrchestrateAsync`

Provider abstraction invoked:

- `IAIProvider.GenerateAsync`

### B. Copilot suggestions

Controller: `CopilotController.SuggestReply` -> `CopilotService.SuggestHostReplyAsync` actually maps to the “suggest-reply” endpoint; the host suggestion flow is in `CopilotService.GetSuggestedRepliesAsync`.

Call chain:

- `backend/Controllers/CopilotController.cs` -> `SuggestReply` or `GetSuggestedReplies`
- `backend/Services/CopilotService.cs` -> `SuggestHostReplyAsync` (generated reply) or `GetSuggestedRepliesAsync` (suggestions list)
- `backend/Services/SubscriptionEntitlementService.cs` -> `AdmitCopilotOperationAsync`
- `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs` -> `OrchestrateAsync`
- `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs` -> `InvokeProviderWithTimeoutAsync`
- `backend/Services/IAIProvider.cs` -> `GenerateAsync`
- `backend/Services/OpenAIAIProvider.cs` / `DevelopmentAIProvider.cs`

Commercial admission location:

- `CopilotService.GetSuggestedRepliesAsync` and `CopilotService.SuggestHostReplyAsync` call `subscriptionEntitlementService.AdmitCopilotOperationAsync(companyId, operationId, conversationId, userId, CopilotOperationType.CopilotSuggestion/GeneratedReply)` before orchestration.

Outer operation identity:

- `CopilotOperation.Id`, created in `SubscriptionEntitlementService.AdmitCopilotOperationCoreAsync`.

AI orchestrator/service invoked:

- `AIReplyOrchestrator`

Provider abstraction invoked:

- `IAIProvider.GenerateAsync`

### C. Copilot generated reply

Controller: `CopilotController.SuggestReply`

Call chain:

- `CopilotController.SuggestReply`
- `CopilotService.SuggestHostReplyAsync`
- `SubscriptionEntitlementService.AdmitCopilotOperationAsync`
- `AIReplyOrchestrator.OrchestrateAsync`
- `AIReplyOrchestrator.InvokeProviderWithTimeoutAsync`
- `IAIProvider.GenerateAsync`

The same provider boundary as above, but the outer operation type is `CopilotOperationType.CopilotGeneratedReply`.

### D. Host workspace draft

Controller: `HostCopilotController.GenerateDraft`

Call chain:

- `backend/Controllers/HostCopilotController.cs` -> `GenerateDraft`
- `backend/Services/HostCopilot/HostCopilotWorkspaceService.cs` -> `GenerateDraftAsync`
- `backend/Services/SubscriptionEntitlementService.cs` -> `AdmitCopilotOperationAsync(companyId, request.OperationId, conversationId, userId, CopilotOperationType.WorkspaceDraft)`
- `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs` -> `OrchestrateAsync`
- `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs` -> `InvokeProviderWithTimeoutAsync`
- `backend/Services/IAIProvider.cs` -> `GenerateAsync`

---

## 3. Outer operation identity: source evidence and durability

### Guest message identity

Current code uses the persisted `ConversationMessage.Id` as the guest outer identity in practice:

- `ChatService.SendGuestMessageAsync` stores the guest message first.
- `ConversationService.AddGuestMessageAsync` creates `new ConversationMessage { Id = Guid.NewGuid(), ... }` before any AI orchestration.
- `ChatService.SendGuestMessageAsync` then admits quota with: `ai-request:guest-message:{guestMessage.Data.Id:N}`.

This means:

- created before first provider call: yes
- survives replay: yes, replay is keyed by `guestMessage.Data.Id` / duplicate detection in `ConversationService.FindDuplicateAsync` and `ConversationRepository` logic
- survives provider failure: yes, the message record already exists
- survives application retry: yes, if the same user message is retried, the persisted message and quota idempotency keys remain the same

No guest commercial path reaches the provider before the message row exists.

### Host Copilot operation identity

Current code uses `CopilotOperation.Id` as the host outer identity:

- `SubscriptionEntitlementService.AdmitCopilotOperationAsync` validates and creates `new CopilotOperation { Id = operationId, ... }` before provider invocation.
- `CopilotService.GetSuggestedRepliesAsync` and `SuggestHostReplyAsync` call `AdmitCopilotOperationAsync` before `AIReplyOrchestrator.OrchestrateAsync`.
- `HostCopilotWorkspaceService.GenerateDraftAsync` does the same before orchestration.

This means:

- created before first provider call: yes
- survives replay: yes, the service checks `dbContext.CopilotOperations.FirstOrDefaultAsync(item => item.Id == operationId)` and treats it as a replay or conflict based on the identity and same company/conversation/user/type
- survives provider failure: yes, the operation row exists and receives `Status = Failed` via `MarkCopilotOperationFailedAsync`
- survives application retry: yes, the idempotency key is based on `operationId` and the service rejects conflicting reuses

No host commercial path reaches the provider before the Copilot operation row exists.

### Result

The design target is supported for the main commercial paths, but the provider abstraction itself does not yet carry or record this identity. It is available in the application service boundary only.

---

## 4. Provider invocation inventory

### Production invocation sites

| File | Class / method | Caller | Classification | Provider method | TokenUsage returned | Outer commercial identity available |
|---|---|---|---|---|---|---|
| backend/Services/AI/Orchestration/AIReplyOrchestrator.cs `InvokeProviderWithTimeoutAsync` | AIReplyOrchestrator | AIReplyOrchestrator.OrchestrateAsync | COMMERCIAL OUTER CALL | `IAIProvider.GenerateAsync` | Yes, when provider returns `AIProviderResult.Success` or failure with `TokenUsage` | No explicit identity; only `CorrelationId` available |
| backend/Services/AI/Orchestration/GroundedConciergeResponseGenerator.cs `GenerateAsync` | GroundedConciergeResponseGenerator | AIReplyOrchestrator.OrchestrateAsync | INTERNAL SUB-OPERATION | `IConciergeLanguageModel.GenerateAsync` | Not in current result type; `ConciergeLanguageModelResult` includes `TokenUsage` in some records but the call path does not aggregate it | No explicit outer identity |
| backend/Services/AI/Orchestration/DevelopmentConciergeLanguageModel.cs `GenerateAsync` | DevelopmentConciergeLanguageModel | GroundedConciergeResponseGenerator | DEVELOPMENT / INTERNAL | provider-like generated output without external API | No token usage | No outer identity |
| backend/Services/AIOrchestrator.cs `ProcessAsync` | AIOrchestrator (legacy) | legacy controller or service callers | LEGACY / DEPRECATED | `IAIProvider.GenerateAsync` | Yes, via `AIProviderResult.TokenUsage` | No explicit outer identity |
| backend/Services/OpenAIAIProvider.cs `GenerateAsync` | OpenAIAIProvider | IAIProvider implementation | COMMERCIAL OUTER CALL | `IOpenAIResponsesClient.CreateResponseAsync` | Yes, on successful mapping and failure responses with usage object | No explicit outer identity; only `request.CorrelationId` |
| backend/Services/OpenAIResponsesClient.cs `CreateResponseAsync` | OpenAIResponsesClient | OpenAIAIProvider | COMMERCIAL OUTER CALL | `ResponsesClient.CreateResponseAsync` | Yes, mapped via `MapTokenUsage(response.Usage)` | No explicit outer identity |
| backend/Services/DevelopmentAIProvider.cs `GenerateAsync` | DevelopmentAIProvider | selected by `AIProviderOptions.Provider` | NON-COMMERCIAL / DEVELOPMENT | deterministic local generation | No token usage currently returned | No outer identity |

Notes:

- `IConciergeLanguageModel` is effectively a provider-like interface for internal AI wording generation. It is not yet part of the commercial outer identity design.
- The old `AIOrchestrator` path is legacy/deprecated (`AIOrchestrationController` returns 410 Gone). It still contains a direct provider call but is not active for customer traffic.

---

## 5. Guest Chat call graph

Observed path:

- `ChatController.SendMessage`
- `ChatService.SendGuestMessageAsync`
- validate guest / create or find conversation
- `ConversationService.AddGuestMessageAsync` creates `ConversationMessage.Id`
- `ChatService` calls `EnsureFeatureEnabledAsync` and `ConsumeQuotaAsync` with `UsageMetric.AiRequests`
- `AIReplyOrchestrator.OrchestrateAsync`
  - context build
  - intent detection
  - knowledge ranking/RAG
  - prompt build
  - optional grounded concierge internal model generation
  - provider invocation via `IAIProvider.GenerateAsync`
  - validation and safety
  - deterministic fallback if validation/safety/context fail

Possible provider calls:

- minimum: 1 provider call when the generation path is used and no grounded internal generation path wins
- additional: 1 internal `groundedConciergeResponseGenerator.GenerateAsync` can happen before the outer provider call, but current code does not accumulate that into the same outer telemetry record
- maximum: statically bounded by the orchestration logic, but not fully fixed at compile time because it branches and may call the internal grounded generator and then the actual provider or fallback; the code is therefore effectively unbounded at the flow level if future internal generators are added.

Current practical bound in this code:

- `groundedConciergeResponseGenerator` may run before provider call, but only once; then `InvokeProviderWithTimeoutAsync` calls the primary provider exactly once; fallback is deterministic and not a paid provider call.

Therefore, for the current implementation, the practical maximum is effectively: 1 real provider call + 1 internal provider-like generation call, but the architecture does not preserve both in one aggregate.

---

## 6. Copilot suggestions call graph

Observed path:

- `CopilotController.GetSuggestedReplies` or `SuggestReply`
- `CopilotService.GetSuggestedRepliesAsync` / `SuggestHostReplyAsync`
- `SubscriptionEntitlementService.AdmitCopilotOperationAsync` creates `CopilotOperation.Id`
- `AIReplyOrchestrator.OrchestrateAsync`
  - context and ranking
  - prompt build
  - possible grounded internal model generation
  - provider invocation via `IAIProvider.GenerateAsync`
  - validation, safety, fallback
- `CopilotService` marks `CopilotOperation` completed/failed

Minimum provider calls:

- 1 call to the active AI provider when generation is successful

Additional provider calls:

- 0 or 1 optional internal provider-like generation call via `groundedConciergeResponseGenerator`
- 0 extra provider call for fallback if the code chooses deterministic fallback; no second paid provider call is made

Retry behavior:

- No explicit application-level retry on the provider call itself in the orchestrator; there is a single call wrapped in timeout cancellation and exception handling
- `QuotaAdmissionRetryPolicy` exists for DB quota races, not for provider retries

---

## 7. Generated reply call graph

Same as suggestions, but the operation is `CopilotGeneratedReply` and the output is a single generated draft rather than a list.

Minimum provider calls:

- 1 real provider call if generation is attempted and successful

Additional provider calls:

- 0 or 1 internal grounded model call before the provider call
- deterministic fallback after failure; no additional paid provider call

`CopilotOperation.Id` availability:

- available before `AIReplyOrchestrator.OrchestrateAsync` due to `AdmitCopilotOperationAsync` call

---

## 8. Workspace draft call graph

Observed path:

- `HostCopilotController.GenerateDraft`
- `HostCopilotWorkspaceService.GenerateDraftAsync`
- `SubscriptionEntitlementService.AdmitCopilotOperationAsync` with `CopilotOperationType.WorkspaceDraft`
- `AIReplyOrchestrator.OrchestrateAsync`
- optional internal grounded generation
- provider call
- validation and fallback
- `MarkCopilotOperationCompletedAsync` or `MarkCopilotOperationFailedAsync`

Minimum provider calls:

- 1 if LLM wording is enabled and generation proceeds

Additional provider-like calls:

- 1 internal grounded generation may run before the main provider if conditions allow

This path is intentionally aligned with the same orchestrator as host-generated replies.

---

## 9. Internal AI sub-operations

### Current internal AI sub-operations

- `GroundedConciergeResponseGenerator.GenerateAsync`
- `DevelopmentConciergeLanguageModel.GenerateAsync`
- retrieval and context classification components do not call external providers; they are deterministic or local heuristics

Should their usage logically contribute to the outer commercial operation?

- `GroundedConciergeResponseGenerator`: UNCLEAR from the current code, but likely YES if it is the same commercial AI response path and it is orchestrated inside the same user operation.
- `DevelopmentConciergeLanguageModel`: NO for actual production telemetry because it is a development-only path and not a real external provider with token reporting; in production it is effectively a deterministic provider stub.

The important architectural detail: the current code executes the grounded internal generation as part of a single outer user operation, but it does not record it as a separate provider attempt with an outer-call identity and aggregate semantics. It is therefore not yet distinguishable in telemetry.

---

## 10. Fallback topology

The code has several layers:

### A. Deterministic / non-provider fallback

- `AIReplyFallbackProvider.BuildFallback(...)` returns a static string when validation, safety, or context quality fails.
- This does not call an external provider.
- Trigger: validation failure, safety block, empty output, insufficient context, conflict, fallback policy enabled.
- First provider result available? not necessarily.
- First provider usage available? only if the provider returned success and usage before fallback.
- Second provider invoked? no.
- Current code preserves both? no; it only sets a boolean `FallbackUsed` and final output.

### B. Paid provider call to another paid provider call

- current code does not implement a second paid API call as a fallback to another paid provider call.
- The design is one provider attempt per orchestration flow, with deterministic fallback instead of provider-to-provider fallback.
- Therefore no explicit “provider fallback usage stack” exists.

### C. Retry of the same provider operation

- there is no application-level retry logic for provider generation in `AIReplyOrchestrator` or the OpenAI adapter.
- there is a timeout wrapper and exception handling, but no retry loop.
- `OpenAIResponsesClient` has no retry policy or `Polly` wrapper.

### D. Internal AI call as a different purpose

- `GroundedConciergeResponseGenerator` is a separate internal generator path that may generate output before the main provider call.
- It is not a separate commercial operation; it is part of the same orchestration flow.

### Key finding

The current system does not preserve multiple paid provider results in one outer operation. It preserves only the final orchestrated output and a final provider result object, not the full set of attempt records.

---

## 11. Failure before response: can the system know usage?

For provider failures before `AIProviderResult.Success` is produced:

- `OpenAIAIProvider.GenerateAsync` catches timeouts, cancellations, request exceptions, and provider exceptions.
- The method returns `AIProviderResult` with `Outcome = Unavailable or Failed`, a failure category, and sometimes `TokenUsage` if a partial provider response object is available.
- If a network or SDK exception happens before any `ResponseResult` is obtained, there is no `TokenUsage` and no provider request ID is available.

The architecture distinguishes between:

- no provider call made: e.g. the code checks for a bad feature gate or missing conversation and never invokes `IAIProvider.GenerateAsync`
- provider call attempted but usage unknown: e.g. `OpenAIProviderException`, `HttpRequestException`, `TimeoutException` after attempt begins but before usage mapping is available

This distinction is present only in the `Outcome`/`FailureCategory` fields, not as a separate durable provider-call record. It is therefore not enough for aggregation semantics across multiple attempts.

---

## 12. Failure after response and before completion

Examples:

- parsing or validation failure after provider returns a successful `ResponseResult`
- database save failure when storing the AI response message
- post-processing failure in `AIReplyOrchestrator`
- HTTP response construction failure after generation but before the client sees the result

This can happen because:

- `AIReplyOrchestrator` sets `providerResult` from the provider response,
- then validation and safety happen,
- then the result is persisted via `conversationService.AddAIMessageAsync`,
- then the HTTP response is returned.

If the application fails after the provider response is received but before the operation completes, current code may lose the provider usage if nothing retains it beyond the in-memory `providerResult`. There is no durable `ProviderCallId` and no outer-operation aggregate object. This means:

- repeated processing can re-invoke the provider if the same outer operation is replayed or retried;
- duplicate or double-counting is possible without a distinct provider-call identity;
- the final result is not a safe telemetry record unless the call is keyed by provider attempt.

---

## 13. Retry layers in the current architecture

### Application service retries

- none for AI provider generation in the relevant customer paths
- quota admission uses `QuotaAdmissionRetryPolicy` for concurrency, not AI provider retries

### Orchestrator retries

- none
- there is timeout cancellation, not a retry loop

### Fallback wrapper

- deterministic fallback only, not provider retry

### HTTP resilience / Polly

- no `AddPolicyHandler`, `WaitAndRetry`, or `HttpClient` resiliency config found for the OpenAI client

### OpenAI client configuration

- `OpenAIResponsesClient` is created as `new ResponsesClient(apiKey)` with timeout only via a local `CancellationTokenSource` in `OpenAIAIProvider`

### SDK-internal retry behavior

- not established locally from the codebase; current source does not show any explicit SDK retry settings
- therefore: UNKNOWN for SDK-mode retries

---

## 14. Provider call vs provider attempt

The current architecture can observe only the StayFlow-level call boundary, not the underlying network or SDK retry boundary.

- StayFlow sees one call to `IAIProvider.GenerateAsync` in the orchestration code.
- The OpenAI SDK may internally retry HTTP calls beneath that abstraction, but the app code does not have a stable, durable way to observe those attempts separately.
- Therefore the application cannot currently distinguish a logical provider call from an SDK-level physical HTTP attempt without a StayFlow-owned provider-call identity.

This is why the design must separate:

- outer commercial operation identity: `ConversationMessage.Id` or `CopilotOperation.Id`
- provider-call identity: a StayFlow-generated attempt ID assigned at the point of each provider invocation attempt

---

## 15. Current token-usage lifetime

### Guest Chat

- `OpenAIResponsesClient.MapTokenUsage` creates `AiTokenUsage` from `ResponseTokenUsage`
- `OpenAIAIProvider.GenerateAsync` returns `AIProviderResult.Success(..., tokenUsage)`
- `AIReplyOrchestrator` stores `providerResult` in an in-memory local variable
- `ChatService` maps that into `AIOrchestrationResult.ProviderMetadata` and persists an AI message, but does not persist token usage in the `ConversationMessage` row
- the usage is discarded when the orchestration method returns or when the response is built

Loss boundary: `AIReplyOrchestrator` / `ChatService` boundary; there is no durable or aggregated telemetry record.

### Host copilot flows

- same pattern: `AIReplyOrchestrator` receives `AIProviderResult.TokenUsage` and then discards it once the response object is returned and the Copilot operation is marked completed/failed
- any provider result is not kept in a durable outer aggregate object

### HTTP response exposure

- the HTTP responses currently expose only `Provider`, `ModelName`, `RequestId`, `DurationMs`, not token usage
- there is no global exposure of `AiTokenUsage` to the client or service layer

---

## 16. Aggregation requirements

Based on the actual call graphs, Phase 2B.2 needs to support these cases:

- single known usage: REQUIRED NOW
- multiple known usages: REQUIRED NOW
- known + unknown: REQUIRED NOW
- all unknown: REQUIRED NOW
- known zero: REQUIRED NOW
- provider failure with unknown usage: REQUIRED NOW
- fallback after known usage: POSSIBLE
- fallback after unknown usage: POSSIBLE
- replayed outer operation: REQUIRED NOW for idempotency and telemetry correctness
- repeated internal provider call: POSSIBLE / needs explicit semantics

The current architecture cannot safely support these without a provider-call identity and explicit aggregation state, because multiple attempts are not recorded separately.

---

## 17. Aggregation semantics

The most defensible semantics for the current architecture is:

- aggregate every provider result with a known usage belonging to the same outer operation, keyed by provider-call identity
- preserve partial / partial-unknown states rather than assuming the final result is the full total
- treat `AiRequests` as billing admission and do not fold token telemetry into quota admission

The strongest candidate is:

- D. Aggregate provider usage according to outer operation and distinct provider-call identity.

This is more defensible than A/B/C because token telemetry is intended to explain provider consumption and cost, not merely final customer-visible text.

---

## 18. Unknown usage aggregation

For combinations such as:

- known 100 + known 50 -> aggregate = 150 each total, fully known
- known 100 + unknown -> cannot compute a single numeric total without marking partial state
- unknown + unknown -> total remains unknown; use a completeness flag or state
- known zero + known 50 -> aggregate = 50 total, but known zero is not the same as unknown

A numeric aggregate alone is misleading if any usage is unknown. The architecture should therefore have a completeness state such as:

- COMPLETE
- PARTIAL
- UNKNOWN

The code currently has no such state because it only stores a single `AiTokenUsage?` or `AIProviderResult` value for each successful call. If Phase 2B.2 is to be safe, it needs a completeness flag or equivalent at aggregation time.

---

## 19. Overflow / numeric safety

`AiTokenUsage` currently uses `long` for input/output/total tokens.

This is safe only if the system enforces checked arithmetic or saturating semantics on aggregate totals. Without this, a multi-call aggregate could overflow. The design should therefore define:

- checked arithmetic at aggregation time; or
- overflow policy such as saturate or reject as impossible

The current code does not implement such a policy. Phase 2B.2 should explicitly define it before persistence.

---

## 20. Provider-call identity requirements

Provider-call identity is required for idempotent aggregation because:

- fallback may produce a second provider call under the same outer operation
- application retry may reprocess the same outer operation
- replay or duplicate processing can happen for the same `ConversationMessage.Id` or `CopilotOperation.Id`
- post-response failure can replay or re-save data after the provider result is known
- future async persistence will require unique per-attempt records

Therefore: YES, provider-call identity is required.

---

## 21. Earliest correct identity generation point

Recommended creation point: at the application/service boundary before the provider invocation is attempted, not after the response.

Reasoning:

- the outer operation is already known at the commercial service level
- each provider attempt needs a unique identifier even when no provider response ID is available
- it should be created before the OpenAI call and before any possible fallback or retry branch
- it is provider-neutral and is compatible with future persistence, logs, and traces

The earliest correct creation point is therefore:

- commercial service for guest chat (`ChatService.SendGuestMessageAsync` after the guest message row exists)
- commercial service / entitlement layer for host Copilot operations (`SubscriptionEntitlementService.AdmitCopilotOperationAsync` before orchestration)

Not recommended:

- provider abstraction alone, because it lacks outer-operation awareness
- OpenAI adapter alone, because it still cannot represent a failed or pre-response attempt cleanly

---

## 22. Identity shape

Recommended shape: a StayFlow-owned opaque identifier, not just the provider request ID.

Why not only provider request ID:

- the provider request ID is not present before the provider call
- some failures never produce a provider response ID
- the same outer operation may include multiple provider attempts
- the system needs persistence and aggregation beyond provider-native metadata

Recommended approach:

- `ProviderCallId`: GUID or stable opaque string
- outer correlation: `OuterOperationType` + `OuterOperationId` + `ProviderCallId`
- keep provider request ID as metadata, not as the primary identity

---

## 23. Outer/inner correlation

Minimal conceptual relationship:

- `OuterOperationType` (GuestMessage or CopilotOperation)
- `OuterOperationId` (`ConversationMessage.Id` or `CopilotOperation.Id`)
- `ProviderCallId`

A single generic outer ID is not sufficient because the same GUID namespace could collide across different outer types; the discriminator is required for clear correlation and future persistence.

---

## 24. Replay and idempotency

### Guest replay

- same `ConversationMessage.Id` replay: the message layer has duplicate detection and idempotency logic
- if the same user message is replayed, the system should not create a second provider attempt for the same outer operation if the intended semantics are to dedupe commercial operations

### Host replay

- same `CopilotOperation.Id` replay: the entitlement service checks for existing row and conflicts if mismatched
- this is a strong idempotency primitive for host operations and should be reused for provider-call dedupe as well

Critical distinction:

- same outer operation replay: should be deduplicated against prior provider call telemetry
- legitimate second provider call inside the same outer operation: must be allowed only if it is a genuinely distinct provider-call ID and not merely a replay of the same call

---

## 25. Concurrency

The current code protects quota admission and deduplication at the outer operation boundary, but not necessarily the provider-call telemetry layer.

Guest concurrency:

- `ChatService` calls `ConsumeQuotaAsync` keyed by guest message ID; this prevents duplicate quota charging but not necessarily duplicate provider execution at the telemetry level unless the same outer-operation replay is blocked earlier

Suggestions/generated reply/workspace draft concurrency:

- `AdmitCopilotOperationAsync` uses `CopilotOperation.Id` and serializable transaction logic to prevent duplicate operation admission
- but this is still separate from provider-call telemetry and does not by itself prevent a second provider call if the same operation is replayed by a different thread or execution path without a distinct provider-call ID

Therefore, the current admission does not guarantee deduplicated provider telemetry; it mainly guarantees quota and outer-operation idempotency.

---

## 26. Observability boundary

Current logging around provider calls is centered on `CorrelationId`, provider name, model, outcome, and duration. This is useful for operator debugging but not sufficient for durable telemetry because it is not attached to the outer operation and provider-attempt identity.

Recommended future correlation:

- attach `ProviderCallId` to logs and traces
- keep logs/traces correlated to durable telemetry, but do not make observability the source of truth for billing
- use `ProviderCallId` as a cross-cutting correlation key, while `AiRequests`/quota logic remains separate

---

## 27. Provider request ID

OpenAI SDK path:

- `OpenAIResponsesClient.CreateResponseAsync` uses `ResponseResult response = await responsesClient.CreateResponseAsync(options, cancellationToken);`
- `RequestId = response.Id`
- `TokenUsage = MapTokenUsage(response.Usage)`

Available:

- before call: no
- after success: yes via `response.Id`
- after failure: not necessarily; may be absent if no response object was returned

As metadata only: yes, it is useful but not sufficient as the primary identity.

---

## 28. Model / provider attribution

Current provider metadata is known at the provider boundary:

- provider: `OpenAIAIProvider.ProviderName = "OpenAI"` or `DevelopmentAIProvider` returns `"Development"`
- requested model: `OpenAIOptions.Model`
- returned model: in `OpenAIResponsesClient`, `ModelName = request.Model` (the call uses the configured model)
- operation type: in orchestrator request `AIReplyOperation` and `CopilotOperationType`

This is enough to support future persistence and later cost analytics, as long as provider-call identity and aggregate state are added.

---

## 29. Proposed runtime contract for Phase 2B.2

Minimum conceptual runtime envelope:

- `ProviderCallContext`
  - `ProviderCallId`
  - `OuterOperationType`
  - `OuterOperationId`
  - `ProviderName`
  - `RequestedModel`
  - maybe `AttemptNumber`
  - maybe `CreatedAtUtc`

- `ProviderCallAttemptResult`
  - `ProviderCallId`
  - `ProviderName`
  - `ModelName`
  - `TokenUsage?`
  - `UsageCompleteness`
  - `Outcome`
  - `FailureCategory?`
  - `RequestId?`

This remains runtime-only and does not require schema persistence yet.

---

## 30. Proposed aggregate contract

Recommended conceptual aggregate:

- `AiTokenUsageAggregate`
  - `KnownInputTokens`
  - `KnownOutputTokens`
  - `KnownTotalTokens`
  - `UsageState` (COMPLETE / PARTIAL / UNKNOWN)
  - maybe `DistinctProviderCallCount`

This is necessary because the current architecture proves that partial/unknown state can occur. Numeric totals alone are not safe enough for telemetry.

---

## 31. Where aggregation should live

Best layer: dedicated aggregation component or orchestrator-owned aggregator that sits above the provider abstraction but below persistence.

Reasoning:

- knows the outer operation identity
- sees multiple provider attempts and fallback branches
- can combine provider results in the same commercial operation
- remains provider-neutral
- is easier to test than embedding aggregation in individual providers

Not ideal:

- provider adapter alone: too low-level; lacks outer operation context
- provider abstraction alone: too provider-specific and too early in the pipeline
- commercial service alone: can work if thin, but becomes less reusable and less provider-neutral

The most robust design is a dedicated aggregation component invoked by the commercial orchestrator/service after each provider attempt.

---

## 32. Relationship to `AiRequests`

`AiRequests` admission is separate and should remain so.

The current code shows clearly:

- guest: quota admission in `ChatService.SendGuestMessageAsync`
- host: quota admission in `SubscriptionEntitlementService.AdmitCopilotOperationAsync`

Token aggregation must not:

- admit or reject operations
- change `AiRequests` count
- refund `AiRequests`
- consume `AiTokens` quota
- alter plan enforcement

This separation should remain explicit and should not be merged into the same runtime contract as usage quota enforcement.

---

## 33. Phase 2B.2 implementation boundary

Phase 2B.2 should include:

- provider-call ID generation contract
- provider-call context propagation through provider calls
- aggregate type with known/partial/unknown semantics
- aggregation service behavior for outer operation correlation
- propagation through the commercial guest/host flows
- capture of fallback/failed attempts as distinct call records where app-level knowledge exists
- tests for idempotency and per-attempt aggregation

Phase 2B.2 should not include:

- database entity schema
- migration generation
- durable persistence queries
- analytics/reporting semantics beyond runtime aggregate shape

Phase 2B.3 should handle:

- durable table/entity design
- migration and persistence
- analytics queries and reporting
- cost and model attribution analysis

---

## 34. Phase 2B.3 data requirements

For persistence design, Phase 2B.2 must make available at least:

- outer operation type
- outer operation ID
- provider-call ID
- provider name
- requested model / returned model
- timestamps
- usage values (input/output/total) when known
- usage completeness state
- provider request ID when present
- outcome / failure category
- attempt ordering or sequence number
- correlation information for logs/traces

This is justified by the current source and the existing metadata that is already being passed around in `AIProviderResult`, `ConversationMessage`, and `AIProviderMetadata`.

---

## 35. Future test matrix

### REQUIRED

- one known provider call
- two known provider calls in same outer operation
- known + unknown usage
- all unknown usage
- known zero + known usage
- fallback without provider call
- provider exception before response
- post-response failure before completion
- outer replay for same `ConversationMessage.Id`
- outer replay for same `CopilotOperation.Id`
- duplicate processing of same provider-call ID
- different provider-call IDs under same outer operation
- overflow test

### OPTIONAL

- same outer operation with two legitimate internal provider calls
- model attribution mismatch between requested and returned model
- cross-tenant separation for aggregate association

---

## 36. Phase 2B.2 implementation status (runtime-only)

The runtime-only Phase 2B.2 implementation is intentionally limited to in-memory contracts and validation. It does not persist token telemetry, create new entities, add migrations, or disturb `AiRequests` billing or quota admission.

### Final runtime contract

- `ProviderCallId`: a StayFlow-owned opaque identifier generated before provider invocation.
- `OuterOperationType`: `GuestMessage` or `CopilotOperation`.
- `OuterOperationId`: the durable outer operation identifier (`ConversationMessage.Id` or `CopilotOperation.Id`).
- `ProviderCallContext`: wrapper for the above values.
- `AIProviderRequest.ProviderCallId`, `AIProviderRequest.OuterOperationType`, and `AIProviderRequest.OuterOperationId`: carried at the provider boundary.
- `AIProviderResult.ProviderCallId`, `AIProviderResult.OuterOperationType`, and `AIProviderResult.OuterOperationId`: propagated on both success and failure paths.
- `AiTokenUsageAggregation`: in-memory aggregator keyed by provider-call identity and outer operation.

### Duplicate policy

Repeated observations for the same provider-call ID are treated as observations of one logical provider call:

- unknown -> unknown is an idempotent no-op;
- unknown -> known enriches the existing observation once;
- known -> identical known is an idempotent no-op;
- known -> unknown does not downgrade known usage;
- known -> conflicting known values are rejected with an `InvalidOperationException` and the original observation remains authoritative.

Distinct provider-call IDs are aggregated independently. Enrichment validates checked totals before mutating the aggregate, so overflow cannot leave partial state.

### Completeness semantics

The aggregate uses `AiTokenUsageCompleteness` with these semantics:

- `Complete`: all recorded provider calls include known token counts.
- `Partial`: at least one provider call is known and at least one call is unknown.
- `Unknown`: no provider usage is known, or all provider calls are unknown.

Known zero values remain known and are not treated as unknown.

### Grounded-call observability

The provider path now exposes the runtime call identity in the orchestration metadata and result object, which keeps grounded or fallback attempts visible without requiring persistence. This preserves the distinction between:

- no provider invocation
- provider invocation with unknown usage
- provider invocation with known usage
- fallback path after a failed provider attempt

### Runtime replay limitation

This implementation intentionally stays in-memory only. It does not claim to replay or restore past provider-call telemetry across process restarts or database snapshots. The runtime model is correct for local aggregation semantics, but it is not a durable replay source.

### Verification evidence

The runtime contract was validated with focused backend tests covering:

- single known usage aggregate
- multi-call known aggregate
- known + unknown partial aggregate
- all unknown aggregate
- unknown replay idempotence
- unknown-to-known enrichment
- identical known replay idempotence
- known-to-unknown non-downgrade
- conflicting known duplicate rejection and state preservation
- atomic overflow rejection
- distinct provider-call aggregation
- known-zero preservation
- OpenAI provider result propagation of provider-call metadata

The focused verification command was:

```
cd /workspace && dotnet test tests/StayFlow.Api.Tests/StayFlow.Api.Tests.csproj --filter "AITokenAggregationRuntimeTests|OpenAIAIProviderTests"
```

The direct aggregation class passed with 11 successes and 0 failures. The Phase 2B.2 focused suite passed with 122 successes and 0 failures. The full backend suite passed with 1,115 successes and 0 failures. Frontend verification passed with 43 test files and 262 tests, plus typecheck and production build. The EF pending-model check reported no model changes since the last migration.

### Phase 2B.3 requirements

Phase 2B.3 remains the persistence layer and analytics step:

- durable telemetry table/entity design
- migration creation
- provider-call records and outer-operation correlation persistence
- query/reporting model for completeness and aggregated totals
- cost and model attribution analysis beyond the runtime-only contract
- policy for durable replay and idempotency when telemetry is rehydrated

### NOT APPLICABLE

- provider-only data persistence migration tests (deferred to 2B.3)
- full PostgreSQL concurrency matrix (already tracked separately and intentionally deferred here)

---

## 36. Final decision

### Recommended design decision

`ConversationMessage.Id` and `CopilotOperation.Id` are the durable outer identities that are already present before the provider call for the commercial guest and host flows. The missing piece is the provider-call identity and aggregation record keyed by `OuterOperationType + OuterOperationId + ProviderCallId`.

### Recommended Phase 2B.2 output

- create a runtime provider-call identity at the commercial orchestration boundary
- propagate it through the provider abstraction
- record provider attempts and their usage in an aggregate keyed to outer operation + provider-call ID
- keep `AiRequests` quota processing separate from telemetry aggregation
- require completeness state for partial or unknown totals

### Current status

PHASE 2B.2 RUNTIME CONTRACTS IMPLEMENTED — COMMERCIAL OUTER IDENTITY PROPAGATED

---

## 37. Safety check summary

The commercial propagation path is now:

- guest `ChatService.SendGuestMessageAsync`: `ConversationMessage.Id` -> `GuestMessage` outer identity;
- `ConversationAIExchangeService` is registered in DI but currently has no controller, service, worker, hub, reflection, or test caller. It remains baseline-compatible and does not claim a commercial outer identity; its classification is `DORMANT_UNCLASSIFIED` pending an owning operation definition;
- `CopilotService.GetSuggestedRepliesAsync`: admitted `CopilotOperation.Id` -> `CopilotOperation` outer identity;
- `CopilotService.SuggestHostReplyAsync`: admitted `CopilotOperation.Id` -> `CopilotOperation` outer identity;
- `HostCopilotWorkspaceService.GenerateDraftAsync`: admitted `CopilotOperation.Id` -> `CopilotOperation` outer identity;
- `AIReplyOrchestrator.InvokeProviderWithTimeoutAsync`: orchestration outer identity -> `AIProviderRequest`;
- `AIProviderRequest` creates the provider-scoped `ProviderCallId` before `IAIProvider.GenerateAsync`.

Retrieval diagnostics remains an internal orchestration path and does not receive a fabricated commercial outer identity. Grounded concierge generation remains development-only in the current provider factory; its separate internal call is not added to the commercial provider-call aggregate.

Direct propagation tests cover the guest path, suggestions, generated reply, workspace draft, provider-call presence, and provider-call/outer-ID inequality. The direct identity suite passed 46 tests with 0 failures and 0 skips.

No persistence, migrations, frontend, pricing, cost, or `AiTokens` quota changes were made. The audit report remains untracked.

### ConversationAIExchangeService classification

- Interface: `IConversationAIExchangeService`
- Implementation: `ConversationAIExchangeService`
- DI: registered as scoped in `ServiceCollectionExtensions`
- Caller status: `REGISTERED_BUT_CURRENTLY_UNUSED`
- Commercial classification: `DORMANT_UNCLASSIFIED`
- Customer-visible operation: not established by source evidence
- AiRequests admission: none
- Durable commercial identity: none
- Provider contract: `ProviderCallId` is available while nullable outer identity fields remain absent
- Correction: removed the Phase 2B.2 `GuestMessage` / persisted-message-ID assignment from this service and preserved all baseline functional behavior
- Scope: no billing, quota, aggregation, database, migration, or approved commercial-path changes

---

## 38. Baseline advancement revalidation — 2026-10-01

Revalidated against `c9becc02083a1b728dbe4f475aad61aa1d8ff0f1` (`main`, equal to `origin/main`). The previously audited baseline was `9d8bbee93cfff0818654aaf800aa8711a2dd394a`.

The two intervening commits are both Phase 2B.2 implementation commits:

- `f4de25571f3405e98cebde84a5f88d91b08830da`: introduced runtime aggregation, StayFlow-owned provider-call identity, outer-operation propagation, provider token mapping, and initial tests.
- `c9becc02083a1b728dbe4f475aad61aa1d8ff0f1`: refined per-call observation and duplicate semantics, exposed runtime snapshots, and expanded zero-call, failure, and identity coverage.

The baseline delta is 19 files: 13 production files and 6 test files. All are AI telemetry/orchestration or the existing guest/Copilot call-site propagation and its tests. It contains no entitlement implementation, usage persistence, database, migration, frontend, auth/organization, test-infrastructure, or unrelated changes. The current worktree has no tracked source/test diff; the six existing Markdown reports remain untracked, and the index is empty. Thus there are no uncommitted Phase 2B.2 files to reconcile; the direct overlap with the old audit is the already-committed Phase 2B.2 implementation itself.

Phase 2A behavior remains intact: guest admission uses `ai-request:guest-message:{messageId:N}`, Copilot admission uses `ai-request:copilot-operation:{operationId:N}`, and durable Copilot operation lifecycle, replay, exhaustion, and provider-failure charging remain unchanged. Guest, suggestions, generated reply, and workspace draft still use their existing durable outer IDs. `ConversationAIExchangeService` remains registered but caller-free and unclassified, with no fabricated outer identity or AiRequests admission.

Provider-call identity is created before `IAIProvider.GenerateAsync` and remains distinct from outer-operation and correlation identities; provider request IDs remain separately mapped. Aggregation tests confirm unknown replay idempotency, unknown-to-known enrichment, no downgrade, deterministic conflict rejection, independent call IDs, checked atomic overflow, zero observed calls versus observed unknown calls, and known-zero completeness. OpenAI usage mapping preserves null as unknown and non-null zero as known zero. Grounded concierge remains backed by the deterministic development model and is not an additional paid provider call. No new commercial AI path, persistence, AiTokens enforcement, pricing, cost calculation, or migration was introduced.

Fresh validation on `c9becc0`:

- Backend build: passed, 0 warnings and 0 errors.
- Focused contract/integration suite: 136 passed, 0 failed, 0 skipped; SignalR integration: 12 passed, 0 failed, 0 skipped.
- Full backend suite: 1,126 passed, 0 failed, 0 skipped.
- Frontend: 43 files and 262 tests passed; typecheck passed; production build passed with dependency annotation and chunk-size warnings.
- EF pending-model check: no pending model changes.
- `git diff --check`: passed; no validation-created tracked changes.

No persistent PostgreSQL or OpenAI service was contacted. No migration was generated or applied. The Phase 2B.2 commits already exist on `origin/main`; this revalidation did not stage, commit, or push anything. This report remains untracked.
