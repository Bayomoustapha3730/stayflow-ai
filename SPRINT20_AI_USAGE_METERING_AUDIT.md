# SPRINT 20 — AI USAGE & TOKEN METERING AUDIT

## BASELINE

Read-only audit completed against the approved repository state.

- Branch: `main`
- HEAD: `6e5ba6cb0a0c733d1465126e786f026ac0ee0128`
- Upstream: `6e5ba6cb0a0c733d1465126e786f026ac0ee0128`
- Source/test changes before audit: none
- Staged changes: none
- Existing untracked reports preserved and not modified:
  - `SPRINT20_RESERVATION_QUOTA_AUDIT.md`
  - `SPRINT20_RESOURCE_CAPACITY_REPORT.md`
- New report: this file only

No production source, tests, configuration, database, migration, or git state was modified. No AI provider was invoked.

## METRICS

### AiRequests

- Defined by `backend/Models/UsageMetric.cs`.
- Storage value: `AiRequests`.
- Quota entitlement key: `Quota.AiRequests`.
- Generic usage model: `UsageRecord` plus `UsageOperation`.
- Generic quota admission exists in `SubscriptionEntitlementService.ConsumeQuotaAsync`.
- It is actively consumed by selected Chat and Copilot paths, but not every AI-capable path.

### AiTokens

- Defined by `backend/Models/UsageMetric.cs`.
- Storage value: `AiTokens`.
- Quota entitlement key: `Quota.AiTokens`.
- Seeded as a numeric quota, but no production call to `ConsumeQuotaAsync` for `UsageMetric.AiTokens` was found.
- No provider usage fields are carried into the application usage model.
- Current classification: DEFINED BUT UNUSED.

## PLAN LIMITS

Canonical values from `backend/Data/SeedData.cs`:

| Plan | AiRequests | AiTokens |
| --- | ---: | ---: |
| Free | 1,000 | 200,000 |
| Starter | 10,000 | 2,000,000 |
| Professional | 50,000 | 20,000,000 |
| Enterprise | unlimited (`null`) | unlimited (`null`) |

Classification:

- Free, Starter, Professional: both metrics are FINITE in seed data.
- Enterprise: both metrics are marked unlimited.
- In production behavior, `AiRequests` is DEFINED and PARTIALLY USED; `AiTokens` is DEFINED BUT UNUSED.

There were no separate development or production seed limit tables found. Tests use synthetic entitlements, mostly for `AiRequests`; no test evidence establishes production `AiTokens` consumption.

Important plan-data mismatch:

- Backend canonical plans are Free, Starter, Professional, and Enterprise.
- `frontend/src/pages/billingPlans.ts` presents Starter with `2,500 AI requests`, Growth with `20,000 AI requests`, and Scale with `200,000 AI requests`.
- The frontend names and request quantities do not match `SeedData.cs` (Starter 10,000; Professional 50,000; Enterprise unlimited).
- The frontend plan cards do not expose token allowances.
- `backend/Services/BillingService.cs` maps `AiRequestLimit` into `BillingPlanResponse` but does not map an `AiTokenLimit`.

## CUSTOMER-FACING LANGUAGE

Relevant customer-facing wording found:

- `frontend/src/pages/billingPlans.ts`: `2,500 AI requests`, `20,000 AI requests`, and `200,000 AI requests`.
- `frontend/src/pages/PlatformAdminPage.tsx`: `AI Requests 30d` and `AI Tokens`.
- `docs/product/Billing/Plans.md`: general wording about limits for `AI usage`, without a precise request/token definition in the searched content.
- `docs/product/Pricing.md`: `AI usage volume`, without a precise unit.
- `docs/business/Financial-Projections.md`: `AI usage per conversation`.
- `docs/business/Revenue-Model.md`: `AI usage costs` and `OpenAI usage`.
- `docs/architecture/14-monitoring.md`: `AI provider latency, failures, and token usage`.

Classification: AMBIGUOUS, with a strong request-based promise in the frontend plan cards and token usage appearing as an administrative/monitoring concept. The product does not consistently expose or define customer-facing token allowances.

## AI CALL PATH INVENTORY

### Chat guest AI reply

- Files: `backend/Services/ChatService.cs`, `backend/Services/AI/Orchestration/AIReplyOrchestrator.cs`.
- Method: `SendGuestMessageAsync` then `AIReplyOrchestrator.OrchestrateAsync`.
- Purpose: guest conversation response.
- Tenant: `companyId` is resolved from `ICurrentTenantContext` and passed through orchestration.
- Conversation: available.
- Reservation: available through resolved conversation context when applicable.
- Trigger: guest message.
- Provider: selected `IAIProvider`; production selection is OpenAI when `AIProviderOptions.Provider` is OpenAI, otherwise deterministic `DevelopmentAIProvider`.
- Streaming: none; the abstraction returns one `AIProviderResult`.
- Tool/function calls: none in the provider contract. Concierge actions may be handled before model orchestration, but are not provider tool-call rounds.
- Retries: no AI provider retry loop in the provider abstraction; timeout and exception are converted to unavailable/failed results.
- Fallback: deterministic safe fallback content inside `AIReplyOrchestrator`; no fallback provider/model.
- Current quota check: `EnsureFeatureEnabledAsync(AiConcierge)` followed by `ConsumeQuotaAsync(AiRequests, 1, chat:ai-reply:{conversationId}:{guestMessageId})`.
- Current token accounting: none.
- Important boundary: quota is consumed before orchestration, so provider failures and deterministic fallback still consume one request unit. Earlier concierge-action handling and human-takeover paths can return without this AI quota call.

### Copilot suggested replies

- Files: `backend/Services/CopilotService.cs`, `AIReplyOrchestrator`.
- Method: `GetSuggestedRepliesAsync`.
- Purpose: host-generated suggestions.
- Tenant: `ICurrentTenantContext`.
- Conversation: available.
- Provider: same `IAIProvider`/orchestration path.
- Current quota: `ConsumeQuotaAsync(AiRequests, 1, copilot:suggestions:{conversationId}:{correlationId})` before orchestration.
- Current tokens: none.
- Idempotency caveat: the key uses conversation plus correlation ID. If correlation is absent or reused, distinct host actions in the same conversation can be treated as the same generic quota operation.

### Copilot generated host draft

- Files: `backend/Services/CopilotService.cs`, `backend/Services/HostCopilot/HostCopilotWorkspaceService.cs`.
- `CopilotService.SuggestHostReplyAsync` consumes `AiRequests` before orchestration.
- `HostCopilotWorkspaceService.GenerateDraftAsync` directly calls `AIReplyOrchestrator.OrchestrateAsync` when LLM wording is enabled but does not call `ConsumeQuotaAsync`.
- Therefore the host copilot draft endpoint is an AI-capable path that can bypass `AiRequests` enforcement.

### Copilot summary

- File: `backend/Services/CopilotService.cs`.
- `GetSummaryAsync` calls `EnsureFeatureEnabledAsync(HostCopilot)` but builds a deterministic summary; no model provider call and no `AiRequests` consumption.
- This is not a provider-call path in the current implementation.

### Legacy AI orchestration endpoint

- Files: `backend/Controllers/AIOrchestrationController.cs`, `backend/Services/AIOrchestrator.cs`.
- Route: `POST /ai/orchestrate`.
- Trigger: authenticated tenant API request.
- Tenant: `AIOrchestrator` receives tenant context and uses context metadata.
- Provider: `IAIProvider` directly.
- Streaming: none.
- Current feature enforcement: controller has `RequireFeature(FeatureKeys.AiConcierge)`.
- Current quota enforcement: no `ConsumeQuotaAsync(AiRequests)` call in the controller or `AIOrchestrator`.
- Current token accounting: none.
- This is an AI-capable provider path that can bypass `AiRequests` quota while feature entitlement and rate limiting still apply.

### Conversation AI exchange service

- File: `backend/Services/ConversationAIExchangeService.cs`.
- Method: `ProcessGuestMessageAsync` stores a guest message and calls `AIReplyOrchestrator` directly.
- Current quota call: none in this service.
- Registered in DI, but no production caller was found in the controller/service search.
- If later exposed, it would be another path requiring an explicit admission boundary.

### Grounded concierge language model

- Files: `GroundedConciergeResponseGenerator.cs`, `DevelopmentConciergeLanguageModel.cs`, `ConciergeLanguageModelProviderFactory.cs`.
- This path returns `ConciergeLanguageModelResult` and is invoked by `AIReplyOrchestrator` when enabled and applicable.
- Current registered provider factory resolves the development language model.
- No token metadata is returned or captured.
- It is a second internal generation abstraction, but not a second quota admission call.

### Development AI provider

- File: `backend/Services/DevelopmentAIProvider.cs`.
- Deterministic, non-provider simulation.
- Returns generated text and a synthetic request ID.
- No token usage.
- Should not be treated as commercial provider accounting evidence.

### OpenAI provider

- Files: `backend/Services/OpenAIAIProvider.cs`, `backend/Services/OpenAIResponsesClient.cs`, `backend/Services/OpenAIProviderContracts.cs`.
- Provider: OpenAI Responses API through the OpenAI SDK.
- Method: `ResponsesClient.CreateResponseAsync`.
- Input: rendered messages, configured model, and `MaxOutputTokenCount`.
- Streaming: no; one awaited response object.
- Response retained: response text, request ID, model name, incomplete/refusal flags.
- Usage discarded: the code does not read prompt/input, completion/output, total, cached, reasoning, or other usage metadata.
- Retry: no provider retry in this abstraction.
- Failure mapping: 429, 5xx, network, timeout, and cancellation become unavailable provider results; other provider failures become failed results.
- No fallback model/provider is called; orchestration emits deterministic fallback content.

## CURRENT REQUEST ENFORCEMENT

### AiRequests

- Checked: PARTIAL.
- Consumed: Chat guest AI path and two Copilot service methods.
- Admission boundary: before `AIReplyOrchestrator` invocation.
- Idempotency: generic `UsageOperation` idempotency exists, but identity quality differs by caller. Chat includes guest message ID; Copilot uses conversation plus correlation ID.
- Atomicity: quota admission is serializable and internally atomic for the usage record/operation. It is not transactionally coupled to provider execution.
- Failure mode: fail-closed for quota exhaustion; `ChatService` catches `QuotaExceededException` and returns a safe fallback/escalation result rather than exposing HTTP 429 in that path.
- Bypass paths: legacy `AIOrchestrationController`/`AIOrchestrator`; `HostCopilotWorkspaceService.GenerateDraftAsync`; registered `ConversationAIExchangeService` if invoked.
- Provider calls can also be avoided after admission by deterministic orchestration branches, so quota can be consumed without a provider call.

### AiTokens

- Checked: NO.
- Consumed: NO production call found.
- Admission boundary: none.
- Idempotency: no token operation identity is used.
- Atomicity: not applicable.
- Failure mode: no token quota rejection.
- Any AI path can execute without token enforcement.

## CURRENT TOKEN ACCOUNTING

`AIProviderResult` contains only:

- outcome
- response text
- provider name
- model name
- request ID
- duration
- failure category

`OpenAIProviderResponse` contains only response text, request ID, model name, incomplete, and refusal flags. `ConciergeLanguageModelResult` contains output, provider/model/request ID, duration, success/failure/validation fields, source IDs, and raw response placeholder, but no token counts.

Classification: NOT AVAILABLE THROUGH CURRENT APPLICATION ABSTRACTION / AVAILABLE-BUT-DISCARDED AT PROVIDER BOUNDARY. The OpenAI SDK response may expose provider usage metadata, but StayFlow does not read or preserve it.

No streaming response exists, so there is no current streaming usage event to capture. No aggregation exists for retries, fallbacks, or multiple rounds.

## BILLING SNAPSHOT

- Authority: `SubscriptionEntitlementService.BuildSnapshotAsync` reads `UsageRecords` for all non-resource metrics, including `AiRequests` and `AiTokens`.
- Period: exact `TenantSubscription.CurrentPeriodStartUtc` and `CurrentPeriodEndUtc` match.
- Current: current-period `UsageRecord.QuantityUsed`, defaulting to zero when no record exists.
- Limit: matching `PlanEntitlement.QuotaLimit`.
- Remaining: `max(limit - used, 0)` for finite quotas; null for unlimited.
- `BillingService.GetUsageSummaryAsync` maps the snapshot into `UsageSummaryResponse`.
- Organization plan usage also exposes generic quota snapshots through `OrganizationPlanController`.
- Platform admin usage aggregates AI records over a 30-day window for administrative reporting.

Because no provider path writes `AiTokens` records, the billing UI can show `AiTokens = 0` despite real provider calls. `AiRequests` can also under-report when a bypass path invokes AI, and can over-report provider calls when admission occurs before deterministic fallback or provider failure.

## BILLING PERIOD

- Existing active subscriptions use stored `CurrentPeriodStartUtc` and `CurrentPeriodEndUtc`.
- If no active subscription exists, `GetOrCreateCurrentSubscriptionAsync` provisions the canonical Free plan with a calendar-month period.
- Free and trial use the same subscription-period snapshot architecture; trial status does not create a separate token/request usage authority.
- Provider billing events can update current period dates through billing snapshot application.
- `UpdatePlanAsync` changes plan ID without resetting usage or current period.
- New period records are naturally isolated by period start/end.
- Enterprise uses null limits and remains unlimited in the generic snapshot.
- The architecture is clearly period-based, but customer-facing AI wording does not clearly say whether the request numbers are monthly.

## REQUEST SEMANTICS

The source does not have one universal logical AI operation identity.

Current examples:

- Chat: `chat:ai-reply:{conversation.Id:D}:{guestMessage.Id:D}`. This is durable for that stored guest message.
- Copilot suggestions: `copilot:suggestions:{conversationId:D}:{correlationId or none}`.
- Copilot draft: `copilot:draft:{conversationId:D}:{correlationId or none}`.
- Legacy AI orchestration: no quota key because no quota call.

One customer-visible action does not necessarily equal one provider call:

- AI orchestration may produce deterministic responses without calling a provider.
- It may call a grounded concierge model or the generic provider depending on conditions.
- It may apply deterministic fallback after a provider failure, validation failure, safety block, or insufficient context.
- Concierge actions can be handled before the Chat AI reply path.
- No provider tool/function-call loop is represented in the current provider contract.

Current guarantee: the Chat guest-message identity can be reused for generic quota idempotency. Current non-guarantee: all AI endpoints have not adopted a shared durable logical operation key.

## TOKEN CAPABILITY

Pre-call controls:

- OpenAI request sets a configured model and `MaxOutputTokenCount`.
- This is an output cap, not an exact total-token estimate.
- No token estimator, preauthorization, token reservation, or token-budget transaction exists.

Post-call controls:

- Provider usage metadata is not extracted.
- No token usage is recorded.
- No hard token quota can safely be enforced from current source.

If token quota becomes commercial, exact usage is only known after execution and requires a provider-independent usage contract, durable usage event/operation identity, and an explicit overage or preauthorization policy.

## STREAMING

No streaming provider abstraction or streaming controller path was found.

- Provider call begins in `OpenAIResponsesClient.CreateResponseAsync`.
- A complete non-streaming response is awaited.
- There are no chunk, client-disconnect, partial-output, or stream-finalization semantics.
- Current request admission could occur before a future stream, but exact tokens could not be recorded without extending the provider abstraction.

## TOOL CALLING

No OpenAI tool/function-call fields or tool-call rounds exist in `OpenAIProviderRequest`, `OpenAIProviderResponse`, or `AIProviderResult`.

Concierge action orchestration is separate application logic and may handle an action before normal AI generation; it is not represented as model call #1/tool/model call #2 in the provider contract.

There is currently no token aggregation across internal model rounds.

## RETRIES / FALLBACK

- OpenAI provider calls do not retry in `OpenAIAIProvider` or `OpenAIResponsesClient`.
- Timeout/network/provider failures become unavailable/failed results.
- `AIReplyOrchestrator` may produce deterministic fallback text; it does not call a secondary model.
- `ChatService` consumes one request unit before provider execution, so failures and fallback can consume a request.
- Copilot admission similarly occurs before orchestration.
- Since token accounting is absent, retries/fallbacks currently produce no token records.

## BACKGROUND AI

No scheduled/background production path invoking an external AI provider was identified. Reservation lifecycle and guest-journey background services are operational/deterministic paths in the searched source.

Ambiguous future policy: whether automatic classifications, summaries, enrichments, or system-generated responses should consume customer quota. Current product language does not settle this.

## IDEMPOTENCY

Durable candidates in current source:

- `ConversationMessage.Id` for Chat guest messages.
- `Conversation.Id` is available but is too broad by itself for multiple actions.
- `currentTenantContext.CorrelationId` is not guaranteed to be a durable logical operation identity.
- Provider request IDs are generated after dispatch and cannot prevent pre-dispatch double admission.
- Legacy `AIOrchestrator` has no quota operation key.

A shared future key could be based on a durable message/action ID, but the source does not currently provide one for every AI-capable endpoint.

## TENANT ISOLATION

- Chat and Copilot resolve `CompanyId` through `ICurrentTenantContext`.
- `ConsumeQuotaAsync` scopes both `UsageRecord` and `UsageOperation` by `CompanyId` and current period.
- Billing snapshots scope usage records by subscription company.
- AI context builders receive company/tenant IDs and tenant-scoped repositories.
- Legacy AI orchestration uses authenticated tenant context through its context builder and audit path, but does not consume quota.

Status: tenant isolation is present for the existing quota calls and billing reads. Main risk is bypass paths that omit enforcement, not cross-tenant usage lookup.

## FAILURE MATRIX

| Case | Current request behavior | Current token behavior |
| --- | --- | --- |
| Provider succeeds | Chat/Copilot request unit already consumed; bypass paths unmetered | Not recorded |
| Provider 4xx | Request unit remains consumed if admitted; orchestration returns failure/fallback | Not recorded |
| Provider 429 | Request unit remains consumed if admitted; mapped unavailable/fallback | Not recorded |
| Provider 5xx | Request unit remains consumed if admitted; mapped unavailable/fallback | Not recorded |
| Timeout | Request unit remains consumed if admitted; mapped unavailable/fallback | Not recorded |
| Network failure | Request unit remains consumed if admitted; mapped unavailable/fallback | Not recorded |
| Response processing failure | Request unit remains consumed if admitted; fallback/safety path | Not recorded |
| Primary failure/fallback | No secondary provider; deterministic fallback; one pre-call request unit | Not recorded |
| Provider retry | No provider retry currently | Not recorded |
| Tool-call round | No tool-call provider round currently | Not recorded |

This table describes current behavior, not a product recommendation.

## CONCURRENCY

The generic `ConsumeQuotaAsync` uses a serializable relational transaction, current-period `UsageRecord`, unique `UsageOperation` idempotency key, and retry handling for serialization failure, deadlock, and relevant unique races.

Therefore it is structurally suitable for request quota admission at a correctly chosen logical boundary:

- distinct identities competing for the final request unit should admit at most one;
- the same operation key should be idempotent.

Current gaps:

- not every AI path calls it;
- Copilot correlation-based identity may not distinguish every distinct action;
- no token aggregation exists, so concurrent token recording is not implemented.

Real PostgreSQL validation is required before relying on concurrent final-unit behavior in deployment.

## OBSERVABILITY

Existing stored/returned information:

- `UsageRecord`: company, metric, period, quantity used.
- `UsageOperation`: company, metric, idempotency key, period, quantity.
- `AIProviderResult`: provider, model, request ID, duration, outcome/failure category, response text.
- `AIProviderMetadata`: provider, model, request ID, duration.
- Platform admin: `AiRequestsLast30Days` and `AiTokensLast30Days` from `UsageRecords`.
- Logs include provider/model/duration/correlation and orchestration outcome/fallback status.

Not stored:

- input/prompt tokens
- output/completion tokens
- total tokens
- cached/reasoning tokens
- estimated or actual cost
- durable AI usage event with provider/model/token dimensions
- provider request ID in a usage ledger

Later cost analysis would require provider, model, token dimensions, durable operation identity, provider request ID, timing, pricing version, and tenant ownership.

## TEST GAP MATRIX

| Area | Status | Evidence |
| --- | --- | --- |
| AiRequests finite limit | PARTIAL | Generic quota tests and BillingService usage fixture |
| AiRequests unlimited | PARTIAL | Generic quota infrastructure; no focused AI provider admission test |
| AiTokens finite limit | MISSING | No production token consumption |
| AiTokens unlimited | MISSING | Seeded only; no token admission |
| Tenant isolation | PARTIAL | Generic quota tests; AI path-specific coverage limited |
| First logical AI operation | PARTIAL | Chat/Copilot code, no complete shared admission test |
| Same-operation retry | PARTIAL | Generic UsageOperation tests, not all AI paths |
| Distinct operation | PARTIAL | Generic quota behavior, not all AI callers |
| Final request unit | PARTIAL | Generic quota tests, not every AI boundary |
| Concurrent final request unit | MISSING | Requires PostgreSQL validation |
| Provider success | PARTIAL | Provider and orchestration tests; no metering assertion across all paths |
| Provider failure | PARTIAL | Provider/orchestration tests; no product-wide quota policy test |
| Provider timeout | PARTIAL | Development/provider tests; no metering assertion |
| Provider retry | NOT APPLICABLE currently | OpenAI path has no retry loop |
| Provider fallback | PARTIAL | Orchestration fallback tests; no shared billing assertion |
| Streaming | NOT APPLICABLE currently | No streaming path |
| Tool-call multi-round | NOT APPLICABLE currently | No provider tool-call contract |
| Token extraction | MISSING | No token fields or extraction |
| Token aggregation | MISSING | No token records |
| Prior-period isolation | PARTIAL | Generic quota period tests use WhatsApp, not AI token/request end-to-end |
| Current-period snapshot | PARTIAL | BillingService uses current snapshot; AI-specific token/request fixture limited |
| Upgrade | PARTIAL | Generic plan/billing tests; no AI meter-specific test |
| Downgrade | PARTIAL | Generic snapshot behavior; no AI path admission test |
| Development simulation | COVERED/PARTIAL | Development provider and concierge tests exist; no quota policy assertion |
| Background/system AI | NOT APPLICABLE currently | No production provider background path found |
| HTTP quota error | PARTIAL | Global handler contract exists; Chat catches quota and returns safe fallback; no universal AI endpoint contract |
| Frontend quota message | MISSING | Generic billing display; no AI quota-specific error handling |

## MIGRATION ASSESSMENT

Current audit conclusion: NO MIGRATION is required merely to select request-based enforcement if existing `UsageRecord`/`UsageOperation` is reused and every logical operation has a durable key.

Token metering may require a schema extension depending on the approved design:

- usage dimensions for input/output/total tokens;
- provider/model/request metadata;
- a token usage event or extended usage record;
- durable operation identity for provider attempts or logical actions.

Do not create a migration in this audit.

## OPTIONS A-E

### A — Request quota only

- Fits current customer-facing plan wording best.
- Uses existing `AiRequests` seed limits and generic quota infrastructure.
- One logical customer-visible AI operation consumes one unit.
- Provider failures, deterministic fallback, retries, and internal rounds require an explicit policy.
- Does not provide reliable provider cost accounting.

### B — Token quota only

- Fits monitoring/cost language better than current plan-card language.
- Not implementable safely with current abstraction because usage is not captured.
- Requires post-call accounting or preauthorization and provider portability decisions.
- Difficult for customers to understand and predict across models.

### C — Both independently enforced

- Expressive for commercial controls and cost protection.
- Highest complexity and risk: request admission plus post-call token enforcement/preauthorization.
- Requires reliable token metadata and clear customer UI for both limits.
- Current frontend/backend plan language does not consistently support it.

### D — Request quota enforced + token telemetry

- Uses the existing request-based commercial language and infrastructure.
- Adds token accounting later for cost/observability without making uncertain provider token data a hard customer gate.
- Still requires all AI paths to share a request admission boundary and durable identity.
- Most understandable and portable interim product model.

### E — Other

Not supported by current source evidence.

## RECOMMENDATION

Recommendation: D — request quota enforced + token telemetry.

Definition of one customer request:

- one durable customer-visible AI operation admitted at the endpoint/service boundary;
- Chat should use the stored guest `ConversationMessage.Id`;
- Copilot actions need a durable action/request identity rather than a possibly absent correlation ID;
- legacy and host draft paths need to route through the same admission abstraction.

Request consumption:

- consume one `AiRequests` unit once, immediately before the provider-capable operation, after feature/tenant validation;
- do not charge deterministic non-provider summaries or concierge action handling unless product policy explicitly classifies them as AI requests;
- do not charge the same logical operation again on replay.

Failed calls:

- Product decision required. Recommended default: charge an admitted logical request even if the provider fails, because provider capacity was attempted and current Chat/Copilot behavior already admits before execution. If the product promises only successful responses count, admission must be reserved/settled atomically with a compensating policy.

Retries:

- retries for the same durable logical operation do not consume additional request units;
- a distinct customer action consumes a separate unit.

Fallback:

- deterministic fallback after a failed admitted provider call does not consume another request unit.
- a separate fallback provider, if introduced later, should remain within the same logical request unless product policy says otherwise.

Tool rounds:

- internal tool/function-call rounds should remain inside one logical customer request unless product language later says otherwise.
- current source has no provider tool-call contract.

Tokens:

- record input/output/total provider usage as telemetry and cost observability, not as a hard customer quota initially.
- aggregate all provider rounds belonging to one logical operation.
- preserve provider/model/request identifiers when available.

Period reset:

- use the existing subscription current period and `UsageRecord`/`UsageOperation` period scoping.

Unlimited:

- Enterprise/null entitlement remains unlimited for request admission; token telemetry may still be recorded.

Downgrade:

- preserve historical/current usage, clamp remaining to zero when over the new request limit, and reject new admitted logical requests; do not delete or rewrite prior usage.

## PROPOSED IMPLEMENTATION BOUNDARY

Likely files/services:

- `backend/Services/SubscriptionEntitlementService.cs`
- `backend/Services/ISubscriptionEntitlementService.cs`
- a new shared AI usage/admission service or narrow extension around the entitlement service
- `backend/Services/ChatService.cs`
- `backend/Services/CopilotService.cs`
- `backend/Services/HostCopilot/HostCopilotWorkspaceService.cs`
- `backend/Services/AIOrchestrator.cs`
- `backend/Controllers/AIOrchestrationController.cs` only if admission remains at controller boundary
- `backend/Services/ConversationAIExchangeService.cs` if it becomes reachable
- `backend/Services/OpenAIProviderContracts.cs`
- `backend/Services/OpenAIResponsesClient.cs`
- `backend/Services/OpenAIAIProvider.cs`
- AI DTOs carrying provider usage metadata
- billing DTO/service/frontend only after product wording and limits are approved

Admission point:

- one shared service boundary immediately before a provider-capable AI operation, not scattered controller-only calls.
- deterministic-only operations should be classified explicitly before charging.

Logical identity:

- durable stored guest message ID for Chat;
- durable host action/request/message ID for Copilot;
- no independent per-retry random correlation ID.

Quota transaction behavior:

- reuse the existing serializable `ConsumeQuotaAsync` mechanism for request admission where provider failure semantics permit admission-before-call;
- if reservation/settlement semantics are selected, add a narrow AI-specific abstraction rather than altering WhatsApp or reservation semantics.

Token accounting boundary:

- extract provider response usage in `OpenAIResponsesClient`;
- carry usage through `OpenAIProviderResponse`, `AIProviderResult`, and orchestration results;
- aggregate rounds per logical operation;
- persist telemetry in an existing usage-compatible structure only if its schema can represent the needed dimensions; otherwise migration is required after approval.

Error contract:

- quota exhaustion should use the established `QuotaExceededException`, global HTTP 429, and `quota_exceeded`.
- Chat currently catches quota exhaustion and returns safe fallback, so endpoint-specific behavior must be approved rather than assumed.

## RISKS / OPEN PRODUCT QUESTIONS

- Backend seeded AI limits and frontend advertised AI request limits disagree.
- Backend exposes both request and token entitlements, while customer-facing UI exposes request wording only.
- Which AI paths count as commercial customer requests: legacy orchestration, host draft generation, guest chat, Copilot suggestions, and future conversation exchange?
- Should deterministic development/provider responses consume request quota?
- Should deterministic summaries and concierge action handling consume quota?
- Should a failed provider attempt consume a request unit?
- Should a fallback provider, if introduced, share the original request unit?
- What is the canonical durable identity for Copilot actions?
- Is token usage telemetry sufficient, or must customers receive a token allowance?
- Should AI usage display current period or a rolling administrative 30-day window?
- Are plan names/limits in the frontend intentionally stale, or are backend seed limits stale?
- What token pricing/version is needed for cost and gross-margin reporting?

## PRE-DEPLOYMENT POSTGRESQL VALIDATION

After semantics and implementation approval, validate without using the persistent production database:

1. AiRequests final unit with two distinct logical identities.
2. Same logical AI identity concurrent replay.
3. Serialization/deadlock retry behavior for request admission.
4. Provider failure policy and whether admitted usage remains correct.
5. Token telemetry aggregation across provider response rounds when implemented.
6. Tenant isolation under concurrent usage writes.

## RECONCILIATION DETAILS

### Commercial operation classifications

| Entry point | HTTP/API caller | Initiator | Customer-visible | Real provider | Deterministic provider | Current quota | Classification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ChatService.SendGuestMessageAsync` -> `AIReplyOrchestrator` | `POST /chat/message` | Guest | Yes | Yes | Yes | `AiRequests` | BILLABLE_CUSTOMER_OPERATION |
| `CopilotService.GetSuggestedRepliesAsync` -> `AIReplyOrchestrator` | `GET /copilot/conversations/{id}/suggested-replies` | Host | Yes | Yes | Yes | `AiRequests` | BILLABLE_CUSTOMER_OPERATION |
| `CopilotService.SuggestHostReplyAsync` -> `AIReplyOrchestrator` | `POST /copilot/conversations/{id}/suggest-reply` | Host | Yes | Yes | Yes | `AiRequests` | BILLABLE_CUSTOMER_OPERATION |
| `HostCopilotWorkspaceService.GenerateDraftAsync` -> `AIReplyOrchestrator` | `POST /host/copilot/conversations/{id}/draft` | Host | Yes | Yes when LLM wording is enabled | Yes/fallback | None | BILLABLE_CUSTOMER_OPERATION |
| `AIOrchestrator.ProcessAsync` | `POST /ai/orchestrate` | Authenticated API caller | Yes | Yes | Yes | Feature gate only | LEGACY_REQUIRES_DECISION |
| `ConversationAIExchangeService.ProcessGuestMessageAsync` | No production caller found | Internal service contract | Yes if reached | Yes | Yes | None | INTERNAL_ONLY |
| `GroundedConciergeResponseGenerator` | Internal orchestrator stage | Internal | Contributes to outer result | Provider factory, currently Development | Yes | Outer path only | INTERNAL_SUB_OPERATION |
| `DevelopmentAIProvider` / `DevelopmentConciergeLanguageModel` | Provider selection | Development/test or fallback | May produce customer-visible fallback | No | Yes | Inherits outer path | DEVELOPMENT_NON_COMMERCIAL provider |

### Guest Chat identity

`ChatService.SendGuestMessageAsync` persists the guest message through `ConversationService.AddGuestMessageAsync` before it calls `ConsumeQuotaAsync` and `AIReplyOrchestrator`. Therefore `ConversationMessage.Id` exists before admission and is the strongest existing durable identity for the operation.

Supported proposal: `ai-request:guest-message:{ConversationMessage.Id:N}`.

The same stored message can preserve its ID during reprocessing. A repeated HTTP request without the same `ExternalMessageId` creates a new message and new ID. With `ExternalMessageId`, Chat performs a duplicate lookup before creating a second message. One inbound message can route to a concierge action or human-takeover path and avoid normal AI generation; it does not invoke the provider more than once through the normal Chat path.

### Copilot identity

`CorrelationIdMiddleware` accepts `X-Correlation-Id` or generates a GUID per HTTP request. The frontend HTTP client reads response correlation IDs but does not send them. Current Copilot identities are therefore not persisted and are not stable across HTTP replay.

| Operation | Current identity | Stable across retry | Persisted | Suitable for billing idempotency | Recommendation |
| --- | --- | --- | --- | --- | --- |
| Suggestions | Conversation ID + correlation ID | No | No | No | Persisted host operation/action ID required |
| Generated reply | Conversation ID + correlation ID | No | No | No | Persisted host operation/action ID required |
| Workspace draft | No quota identity | No | No | No | Persisted draft-generation operation ID required |

**SCHEMA/API IDEMPOTENCY DECISION REQUIRED.** The current source does not expose a durable pre-provider Copilot action ID. This may be solvable without migration if an existing persisted host action/message can own the operation; otherwise an API idempotency key and persistence are required.

### Legacy `/ai/orchestrate`

The route is authenticated, tenant-scoped through the current tenant context and AI context builder, feature-gated with `AiConcierge`, and capable of invoking the configured real provider. No frontend caller or backend service caller was found, but the route remains reachable and customer/API-visible.

Classification: **DEPRECATED_BUT_REACHABLE**. Do not silently add billing semantics. Decide separately whether to restrict/internalize/remove it or route it through shared admission.

### Host workspace boundary

`HostCopilotWorkspaceService.GenerateDraftAsync` is a separate host-facing endpoint and frontend action. It is not called by the metered `CopilotService` endpoints, so the source does not show an outer-plus-inner double charge for one endpoint action. It is currently unmetered and should be treated as its own billable boundary if retained.

### Conversation AI exchange

`ConversationAIExchangeService` is registered in DI, stores a guest message, and calls `AIReplyOrchestrator`, but no production caller was found. It should not independently consume quota when invoked inside an outer customer operation; the outer boundary must own admission.

### Frontend plan reconciliation

| File | Component | Displayed plan/value | Source | Customer-visible | Backend equivalent | Conflict |
| --- | --- | --- | --- | --- | --- | --- |
| `frontend/src/pages/billingPlans.ts` | `billingPlanCards` | Starter: `2,500 AI requests` | Hardcoded | Yes | Starter: 10,000 | Yes |
| `frontend/src/pages/billingPlans.ts` | `billingPlanCards` | Growth: `20,000 AI requests` | Hardcoded | Yes | No Growth plan; closest Professional: 50,000 | Yes |
| `frontend/src/pages/billingPlans.ts` | `billingPlanCards` | Scale: `200,000 AI requests` | Hardcoded | Yes | No Scale plan; Enterprise unlimited | Yes |
| `frontend/dist/assets/index-CqmJjt5S.js` | Built plan copy | Same values | Generated from source | Yes | Same mismatch | Yes |

The billing dashboard fetches canonical subscription/usage data, but the plan comparison cards ignore API entitlements for their hardcoded marketing values. `BillingService.MapPlanResponse` exposes `AiRequestLimit` but not `AiTokenLimit`. Proposed authority is backend `PlanEntitlement`/`SeedData` until product pricing is reconciled. No values were changed in this audit.

### Billing UI treatment for telemetry-only tokens

Phase 2A should hide `AiTokens` from customer-facing entitlement/limit presentation rather than display a seeded allowance that is not enforced. Administrative `AI Tokens 30d` reporting may remain, but it currently reads `UsageRecords` and will under-report until telemetry exists.

## FINAL SEMANTICS TABLE

| Operation | Initiator | Customer-visible | Billable AiRequest | Logical identity | Current enforcement | Proposed boundary |
| --- | --- | --- | --- | --- | --- | --- |
| Guest Chat AI reply | Guest | Yes | Yes | Guest `ConversationMessage.Id` | Yes | Shared admission after message persistence |
| Copilot suggestions | Host | Yes | Yes | None durable | Yes | Shared admission with persisted operation identity |
| Copilot generated reply | Host | Yes | Yes | None durable | Yes | Shared admission with persisted operation identity |
| Host workspace draft | Host | Yes | Yes | None durable | No | Own shared-admission call |
| Legacy `/ai/orchestrate` | API caller | Yes | Decision required | None | No | Restrict or explicitly meter after decision |
| Conversation AI exchange | Internal/unreferenced | Yes if reached | Outer caller owns it | None | No | Do not meter independently |
| Grounded concierge / fallback | Internal stage | Part of outer result | No additional unit | Outer identity | Inherited | Never independently charge |

## PHASE 2A IMPLEMENTATION BOUNDARY

Likely files: `ChatService.cs`, `CopilotService.cs`, `HostCopilotWorkspaceService.cs`, `SubscriptionEntitlementService.cs`/interface or a narrow shared AI admission service, plus focused tests. Legacy controller treatment must be decided before implementation.

Admission paths: guest Chat, Copilot suggestions, Copilot generated reply, and Host workspace draft if retained as commercial. Identities: guest message ID for Chat; persisted operation/action ID for Copilot/workspace. Explicit exceptions: deterministic-only summaries/actions and internal orchestration stages do not independently charge; development provider behavior is simulation, while a legitimate commercial request may still consume one request.

Token persistence/enforcement is explicitly excluded. Reuse the existing serializable request-quota admission and `UsageOperation` idempotency model where the selected identity supports it. Exhaustion uses `QuotaExceededException`, global HTTP 429, and `quota_exceeded`, subject to existing Chat safe-fallback behavior.

Required tests: every commercial admission boundary, finite/unlimited, tenant isolation, first/distinct/same identity, final-unit concurrency, admitted provider failure policy, no internal double charge, legacy treatment, and no token hard enforcement.

## BLOCKERS BEFORE IMPLEMENTATION

- Reconcile backend canonical plan limits and frontend hardcoded plan names/values.
- Decide whether reachable legacy `/ai/orchestrate` is billable, internalized, or removed.
- Establish a durable Copilot/workspace operation identity; correlation ID alone is insufficient.
- Decide whether deterministic-only customer actions are billable.
- Confirm recommended policy that an admitted provider failure consumes one request unit.
- Decide whether customer-facing UI hides token allowances during telemetry-only Phase 2A.

## FINAL SAFETY

- DefaultConnection used: No
- Persistent PostgreSQL contacted: No
- Persistent DB modified: No
- AI provider contacted: No
- Migration created/applied: No
- Source/test files modified: No
- Staged: No
- Committed: No
- Pushed: No

## PHASE 2A RECONCILIATION BLOCKER

The final persistence inspection confirms that no existing durable Copilot operation identity satisfies the approved requirements for suggestions, generated replies, and workspace drafts.

- `ConversationMessage.Id` is suitable for guest Chat because the message is persisted before AI admission.
- `PendingConciergeAction.Id` and `CreatedFromMessageId` represent guest concierge actions, not host Copilot suggestions or workspace draft generation.
- `ConciergeActionAuditLog.CorrelationId` is an audit correlation field, not a durable Copilot operation identity.
- `HostCopilotDraftGenerateRequest` contains no operation ID, and no persisted draft entity or workspace operation table was found.
- `CopilotService` currently derives quota keys from conversation ID plus request-scoped correlation ID; `CorrelationIdMiddleware` generates a new ID per HTTP request when the client does not provide one.

Therefore Phase 2A cannot safely implement replay-idempotent billing for all approved Copilot operations without introducing a new persistence boundary or an API idempotency contract.

Required approval before implementation:

- minimal persisted Copilot operation entity/table, or
- a verified existing persisted host operation/message identity and its replay contract.

No source or test files were modified during reconciliation.

## AUDIT STATUS

AUDIT COMPLETE — AWAITING AI METERING SEMANTICS APPROVAL

## PHASE 2A IMPLEMENTATION STATUS

An implementation attempt was started after Copilot operation identity approval, but it is not complete and must not be treated as ready for commit.

### Completed implementation areas observed

- `CopilotOperation` entity was added with `CompanyId`, `ConversationId`, `ActorUserId`, `OperationType`, `Status`, `CreatedAt`, `UpdatedAt`, and nullable `CompletedAt`.
- Copilot/workspace request contracts were updated to carry `operationId`.
- Chat, Copilot, and workspace service paths were partially wired to request admission.
- Frontend Copilot/workspace clients were partially updated to generate and pass operation IDs.
- Canonical frontend plan names/values were partially reconciled away from the stale Growth/Scale display.
- Focused AI tests reached a passing state before migration validation: 8 tests passed in the last focused run.

### Blocking schema defect

The generated migration `20260922181212_AddCopilotOperations` contains unintended shadow relationships because `CopilotOperationConfiguration` currently uses `WithMany()` despite existing navigation collections on `Company`, `Conversation`, and `User`.

The migration incorrectly introduces these nullable shadow columns and foreign keys:

- `CompanyId1`
- `ConversationId1`
- `UserId`

It also creates corresponding unintended indexes and foreign keys. The approved schema requires only the explicit required relationships:

- `CompanyId -> Companies.Id`
- `ConversationId -> Conversations.Id`
- `ActorUserId -> Users.Id`

The migration must be regenerated after binding the relationships to the existing navigation collections:

- `Company.CopilotOperations`
- `Conversation.CopilotOperations`
- `User.CopilotOperations`

### Current migration state

- Migration generated: Yes, `20260922181212_AddCopilotOperations`
- Migration applied: No
- Persistent PostgreSQL contacted: No
- Pending model changes: Yes, because the model/configuration and generated migration are not yet in a clean finalized state
- Migration audit: FAILED due to unintended shadow FKs

### Current worktree state

- Branch: `main`
- No staged changes
- No commit or push performed for Phase 2A
- Existing diagnostic reports remain untracked
- AI implementation currently includes 23 modified tracked files and 4 untracked files, including the new Copilot entity/configuration and generated migration artifacts

### Required next repair

1. Bind the three CopilotOperation relationships to the existing navigation collections.
2. Remove/regenerate only the just-created unapplied `AddCopilotOperations` migration.
3. Inspect the regenerated migration and confirm no shadow columns, unrelated schema changes, or extra indexes.
4. Run focused AI tests and the full validation suite.
5. Update this report with final results before any staging decision.

### Final implementation verdict

The EF relationship defect was repaired and the migration was regenerated cleanly.

### CopilotOperation

- Fields: `Id`, `CompanyId`, `ConversationId`, `ActorUserId`, `OperationType`, `Status`, `CreatedAt`, `UpdatedAt`, nullable `CompletedAt`.
- Operation types: `CopilotSuggestion`, `CopilotGeneratedReply`, `WorkspaceDraft`.
- Statuses: `Pending`, `Completed`, `Failed`.
- Billing identity: `ai-request:copilot-operation:{OperationId:N}`.
- Guest Chat continues to use `ConversationMessage.Id`; no guest schema was added.

### Relationship repair

The original shadow FKs were caused by `CopilotOperationConfiguration` using `WithMany()` while `Company.CopilotOperations`, `Conversation.CopilotOperations`, and `User.CopilotOperations` inverse collections existed. EF inferred second relationships and generated `CompanyId1`, `ConversationId1`, and `UserId`.

The configuration now binds each relationship explicitly to the existing inverse collection. The regenerated migration contains exactly one relationship for each approved FK:

- `CompanyId -> Companies.Id`, `Restrict`
- `ConversationId -> Conversations.Id`, `Restrict`
- `ActorUserId -> Users.Id`, `Restrict`

No shadow FK columns remain.

### Migration

- Defective migration removed through `dotnet ef migrations remove`.
- Replacement migration: `20260922185101_AddCopilotOperations`.
- Migration applied: No.
- Table: `CopilotOperations`.
- Columns: approved operation fields plus standard audit timestamps only.
- Indexes: primary key on `Id`, FK-supporting indexes on `CompanyId`, `ConversationId`, and `ActorUserId`, plus the tenant/conversation/actor/type lookup index.
- Unrelated schema changes: None.
- Fresh EF pending-model check after rebuild: no pending model changes.

### Phase 2A implementation

- Chat admission uses the persisted guest message identity.
- Copilot suggestions, generated replies, and workspace drafts require `operationId` and use the shared `AdmitCopilotOperationAsync` boundary.
- New operation creation and `AiRequests` admission are saved in one serializable relational transaction.
- Valid replays reuse the persisted operation and quota operation identity.
- Tenant, conversation, actor, and operation type mismatches are rejected as conflicts.
- Provider execution occurs only after admission succeeds.
- Provider failure marks the operation `Failed` without refunding quota.
- Completed provider execution marks the operation `Completed`.
- `/ai/orchestrate` is no longer provider-capable: it returns HTTP 410 Gone as a deprecated legacy endpoint.
- `AiTokens` remains telemetry-only and is not consumed or enforced.
- Frontend plan presentation was reconciled to Free, Starter, Professional, and Enterprise request semantics; generated `dist` output was not manually edited.

### Validation

- Focused AI backend tests: 109 passed, 0 failed, 0 skipped.
- Full backend: 1,073 passed, 0 failed, 0 skipped; duration 67.3s.
- Frontend: 43 files, 262 passed, 0 failed, 0 skipped.
- Frontend typecheck: passed.
- Frontend production build: passed with non-blocking warnings.
- EF pending-model check: passed with no pending model changes.
- `git diff --check`: passed.

### Remaining validation boundary

Provider exactly-once execution is not guaranteed. A retry of a successfully admitted operation may invoke the provider again, but it cannot consume a second `AiRequests` unit. Real PostgreSQL validation remains required for concurrent same-ID operation creation, distinct final-unit admission, serialization/deadlock retry, and tenant isolation.

### Final implementation verdict

READY FOR PRE-COMMIT AUDIT

## REGRESSION COVERAGE CLOSURE

Coverage-only changes added:

- `tests/StayFlow.Api.Tests/CopilotOperationAdmissionTests.cs`
- `tests/StayFlow.Api.Tests/AIOrchestrationControllerTests.cs`
- Existing stale workspace test payload updated with `operationId`.
- Existing Copilot test call updated with `operationId`.

Production source, migration, entity, and configuration files were not modified during coverage closure.

### Direct invariant results

- CopilotOperation creation: DIRECT PASS
- Same-ID replay: DIRECT PASS
- Different IDs: DIRECT PASS
- Cross-tenant reuse: DIRECT PASS
- Cross-conversation reuse: DIRECT PASS
- Cross-actor reuse: DIRECT PASS
- Cross-operation-type reuse: DIRECT PASS
- Operation before provider: DIRECT PASS through the admission boundary's persisted operation and usage state before the provider callback
- Exhaustion prevents provider: DIRECT PASS; provider callback count remains zero and no operation/usage admission is persisted
- Provider failure remains charged: DIRECT PASS
- Failed-operation replay no second unit: DIRECT PASS
- Correlation ID irrelevant: DIRECT PASS; billing key is derived from CopilotOperation.Id
- Legacy `/ai/orchestrate` 410: DIRECT PASS
- Legacy provider not invoked: DIRECT PASS
- Suggestions/generated reply/workspace operation types: DIRECT PASS through the shared persisted admission boundary
- AiTokens not enforced: DIRECT PASS by source audit; no `ConsumeQuotaAsync(AiTokens)` call exists

### Validation closure

- Focused Phase 2A backend: 121 passed, 0 failed, 0 skipped.
- Full backend: 1,085 passed, 0 failed, 0 skipped; duration 65.9s.
- Frontend: 43 files, 262 passed, 0 failed, 0 skipped.
- Frontend typecheck: passed.
- Frontend production build: passed.
- EF pending-model check: no pending model changes.
- `git diff --check`: passed.
- Nothing staged, committed, or pushed.
- Migration remains unapplied.

### Coverage closure status

COVERAGE CLOSED — READY FOR FINAL PRE-COMMIT AUDIT
