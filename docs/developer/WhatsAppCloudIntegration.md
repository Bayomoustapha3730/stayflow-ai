# WhatsApp Cloud Integration

This document describes the Sprint 12 Part 2 live Meta WhatsApp Business Cloud API integration for StayFlow.

## Scope

StayFlow reuses a single conversation pipeline for web and WhatsApp channels:

`Webhook -> tenant routing -> existing conversation engine -> existing grounded AI/host pipeline -> channel dispatcher -> delivery updates`

This sprint does not create a separate WhatsApp inbox, duplicate delivery pipelines, or bypass existing customer-service-window and template approval rules.

## Production And Development Modes

- Development mode uses `DevelopmentWhatsAppCloudClient`.
- Production mode uses `WhatsAppCloudClient`.
- Development mode is explicitly blocked outside Development environment.
- Simulated credentials are available only when both ASP.NET `Development` and `WhatsAppCloud:DevelopmentMode=true` are set.
- Production defaults keep `WhatsAppCloud:DevelopmentMode=false` and `WhatsAppCloud:ProductionSendingEnabled=false`; enable sending only after the Meta phone number is verified.
- No silent fallback from production mode to development mode exists.

## Meta Prerequisites

1. A Meta app with WhatsApp product enabled.
2. WhatsApp Business Account (WABA).
3. Linked WhatsApp phone number ID.
4. App access token with required messaging and template permissions.
5. App secret for webhook signature validation.
6. Webhook verify token configured in Meta callback setup.

## Credential Reference Model

Credentials are not persisted in the database.

Each integration stores only `CredentialReference` and resolves values from environment-backed keys:

- `STAYFLOW_WHATSAPP_<REFERENCE>_ACCESS_TOKEN`
- `STAYFLOW_WHATSAPP_<REFERENCE>_APP_SECRET`
- `STAYFLOW_WHATSAPP_<REFERENCE>_WEBHOOK_VERIFY_TOKEN`

Reference normalization is server-side and constrained. Browser clients cannot choose arbitrary environment variable names.

Example:

```bash
export STAYFLOW_WHATSAPP_DEFAULT_ACCESS_TOKEN="replace-me"
export STAYFLOW_WHATSAPP_DEFAULT_APP_SECRET="replace-me"
export STAYFLOW_WHATSAPP_DEFAULT_WEBHOOK_VERIFY_TOKEN="replace-me"
```

Use a deployment secret store or workload-injected environment variables for these values. Do not put them in `appsettings*.json`, frontend variables, `.env.local`, database records, logs, support tickets, or screenshots. The repository ignores `.env` files except the intentionally non-secret `.env.production.example` template.

## Backend Configuration

Global settings live under `WhatsAppCloud`:

- `Enabled`
- `GraphApiBaseUrl` (must be HTTPS in production)
- `GraphApiVersion`
- `DefaultCredentialReference`
- `RequestTimeoutSeconds`
- `MaxRetryAttempts` (GET retries)
- `MaxPostRetryAttempts` (conservative POST retries)
- `RetryBaseDelayMilliseconds`
- `RetryMaxDelaySeconds`
- `MaxTemplateSyncPages`
- `MaxTemplateSyncItems`
- `ProductionSendingEnabled`
- `DevelopmentMode`
- `CustomerServiceWindowHours`

Per-company routing values remain on `WhatsAppIntegration`:

- `PhoneNumberId`
- `WhatsAppBusinessAccountId`
- `GraphApiVersion`
- `CredentialReference`

`BusinessPhoneNumberMasked`, connection status, health timestamps, and sanitized failure summaries are organization-scoped display data. WABA IDs, Phone Number IDs, and credential references are server-only integration-routing data; access tokens, app secrets, and webhook verify tokens remain outside the database.

## Live Outbound Sending

### Text messages

`POST /{GraphApiVersion}/{PhoneNumberId}/messages` with:

- `messaging_product=whatsapp`
- `recipient_type=individual`
- `to` normalized recipient
- `type=text`
- `text.body`
- `text.preview_url=false`

### Template messages

`POST /{GraphApiVersion}/{PhoneNumberId}/messages` with:

- `type=template`
- `template.name`
- `template.language.code`
- deterministic ordered body variables

Only approved active templates pass into live provider sends.

## Template Synchronization

Source endpoint:

`GET /{GraphApiVersion}/{WabaId}/message_templates`

Behavior:

- uses provider pagination
- follows only safe `paging.next` URLs constrained to configured Graph origin and expected version path
- bounded by max pages and max items
- deterministic upsert by name/language within tenant integration scope
- deactivation follows existing explicit policy
- raw provider payload is not persisted

## Error Classification

Provider failures are mapped to safe categories:

- `Authentication`
- `Authorization`
- `InvalidDestination`
- `InvalidTemplate`
- `TemplateParameterMismatch`
- `CustomerServiceWindowClosed`
- `RateLimited`
- `TemporaryProviderFailure`
- `ProviderUnavailable`
- `Configuration`
- `Unknown`

Public responses use concise sanitized summaries. Raw provider payloads and secrets are never returned.

## Retry Behavior

- Exponential backoff with jitter.
- Honors `Retry-After` when present.
- Bounded attempts and bounded delay.
- Retry classes: `429`, `500`, `502`, `503`, `504`, transport failures, selected timeouts.
- GET template sync retries are enabled.
- POST send retries are conservative and configurable.
- Cancellation token is respected throughout.

## Rate Limits

`429` is classified as `RateLimited` and mapped to host-safe message:

`WhatsApp is temporarily rate limited. Try again shortly.`

Provider headers/bodies are not exposed to frontend DTOs.

## Integration Health

Health checks validate:

- active integration
- required routing fields
- access token, app secret, and webhook verify token resolution
- provider validation via non-message call (template retrieval)

An integration can be connection-validated while production sending remains disabled. This is the intended state while Meta phone-number verification is pending.

Health statuses:

- `Healthy`
- `ProductionPending`
- `ConfigurationIncomplete`
- `AuthenticationFailed`
- `AuthorizationFailed`
- `RateLimited`
- `ProviderUnavailable`
- `Disabled`

Persistence updates:

- `LastHealthCheckAt`
- `LastSuccessfulHealthCheckAt` (success only)
- `LastErrorSummary` (sanitized)

## Webhook Security

Routes:

- `GET /webhooks/whatsapp` (Meta verification)
- `POST /webhooks/whatsapp` (signed payload)

Security behavior:

- raw body signature validation remains mandatory
- candidate app secrets and verify tokens are resolved from every active integration reference and the default reference, so no tenant is excluded as the integration count grows
- constant-time comparisons are used
- no logging of matched secret identity
- tenant selection is not trusted from payload IDs; routing occurs after signature validation using `phone_number_id`

## Logging And Diagnostics

Structured diagnostics include:

- operation name
- company/integration IDs
- status category
- HTTP status
- attempt count
- elapsed milliseconds
- message type
- short provider support reference (when available)

Never logged:

- access tokens
- app secrets
- webhook verify tokens
- authorization headers
- raw request/response payloads
- full phone numbers
- guest message text
- template variable values

## Webhook Callback URL

Configure Meta with the stable, public production URL:

`https://<production-api-host>/webhooks/whatsapp`

Codespaces and local tunnel URLs are development-only and must not be registered as production callbacks.

## Manual Production Readiness Checklist

1. Set `WhatsAppCloud:Enabled=true`, `DevelopmentMode=false`, and retain `ProductionSendingEnabled=false` until Meta verification completes.
2. Confirm `GraphApiBaseUrl` is HTTPS and configure the stable public webhook callback URL in Meta.
3. Inject each credential-reference variable from the production secret store.
4. Verify each organization integration reports `ProductionPending` or `Healthy` from WhatsApp Settings without exposing credentials.
5. Sync templates and confirm tenant-scoped counts.
6. After phone-number verification, enable the organization integration and `ProductionSendingEnabled`, then validate one approved template send in its tenant scope.
7. Confirm webhook signature validation, inbound deduplication, delivery-status correlation, host escalation, and safe logs in staging.
8. Confirm retry and rate-limit behavior in staging.

## Customer Service Window

- Free-form text sends require an open customer-service window.
- Outside window, only approved templates are allowed.
- Enforcement remains server-side.

## Important Deployment Note

Meta Graph API versions, error codes, and permission requirements can evolve. Validate this integration against current official Meta documentation before production rollout.
