# WhatsApp Cloud Integration

This document describes the Sprint 11 Part 1 WhatsApp Business Cloud API foundation for StayFlow.

## Scope

StayFlow uses WhatsApp only for property-specific guest support, reservation communication, check-in assistance, and host handoff. It is not implemented as an open-domain chatbot.

WhatsApp messages are grounded by the same StayFlow conversation and AI pipeline already used for web chat:

`WhatsApp webhook -> tenant routing -> guest/reservation/property resolution -> existing conversation engine -> existing grounded AI reply orchestration -> channel sender -> Host Inbox realtime updates`

## Architecture

- `Conversation.Channel` continues to identify the communication channel. Sprint 11 reuses the existing `GuestChannel.WhatsApp` value.
- `ConversationMessage` now stores normalized provider delivery fields for outbound channel messages.
- `WhatsAppIntegration` maps a Meta phone number ID to exactly one StayFlow company.
- `WhatsAppWebhookController` verifies Meta webhook traffic and acknowledges quickly.
- `WhatsAppWebhookBackgroundService` dequeues valid webhook payloads for background processing.
- `WhatsAppWebhookProcessor` resolves tenant routing, deduplicates external message IDs, and routes inbound text into the existing conversation engine.
- `ConversationService` still owns message persistence, realtime publication, and conversation state transitions.
- `IConversationChannelDispatcher` routes outbound visible host and AI replies by conversation channel.
- `IWhatsAppCloudClient` sends outbound WhatsApp text through either the real Cloud API client or the deterministic development client.

## Required Configuration

All secrets must come from environment variables or user secrets. Do not commit real values.

Configuration keys:

- `WhatsAppCloud:Enabled`
- `WhatsAppCloud:GraphApiBaseUrl`
- `WhatsAppCloud:GraphApiVersion`
- `WhatsAppCloud:PhoneNumberId`
- `WhatsAppCloud:WhatsAppBusinessAccountId`
- `WhatsAppCloud:AccessToken`
- `WhatsAppCloud:AppSecret`
- `WhatsAppCloud:WebhookVerifyToken`
- `WhatsAppCloud:RequestTimeoutSeconds`
- `WhatsAppCloud:MaxRetryAttempts`
- `WhatsAppCloud:DevelopmentMode`

Suggested local commands:

```bash
cd backend
dotnet user-secrets set "WhatsAppCloud:Enabled" "true"
dotnet user-secrets set "WhatsAppCloud:GraphApiBaseUrl" "https://graph.facebook.com"
dotnet user-secrets set "WhatsAppCloud:GraphApiVersion" "v23.0"
dotnet user-secrets set "WhatsAppCloud:PhoneNumberId" "demo-phone-number-id"
dotnet user-secrets set "WhatsAppCloud:WhatsAppBusinessAccountId" "demo-waba-id"
dotnet user-secrets set "WhatsAppCloud:AccessToken" "replace-me"
dotnet user-secrets set "WhatsAppCloud:AppSecret" "replace-me"
dotnet user-secrets set "WhatsAppCloud:WebhookVerifyToken" "replace-me"
dotnet user-secrets set "WhatsAppCloud:DevelopmentMode" "true"
```

When `WhatsAppCloud:Enabled` is `false`, validation does not require WhatsApp credentials.

## Webhook Verification

Routes:

- `GET /webhooks/whatsapp`
- `POST /webhooks/whatsapp`

Verification rules:

- `GET` expects `hub.mode=subscribe`, `hub.verify_token`, and `hub.challenge`.
- The verification token is compared with a fixed-time comparison.
- `POST` reads the raw request body before deserialization.
- `POST` verifies `X-Hub-Signature-256` with `HMAC-SHA256(AppSecret, rawBody)`.
- Missing or invalid signatures are rejected when WhatsApp is enabled.
- The verified payload is queued for background processing and acknowledged immediately.

Example verification request:

```text
GET /webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=dev-webhook-verify-token&hub.challenge=123456
```

Expected response body:

```text
123456
```

## Tenant Routing

StayFlow does not accept company or property identifiers from the webhook payload.

Routing rules:

1. Read `value.metadata.phone_number_id` from each webhook change.
2. Resolve it through `WhatsAppIntegration`.
3. Reject inactive or unknown integrations.
4. Search active guests only inside that company.
5. Normalize and compare phone numbers with `PhoneNumberNormalizer`.

## Inbound Text Flow

Supported inbound message fields:

- `object`
- `entry[]`
- `changes[]`
- `value.metadata.phone_number_id`
- `value.messages[].id`
- `value.messages[].from`
- `value.messages[].timestamp`
- `value.messages[].type`
- `value.messages[].text.body`
- `value.messages[].context.id`

Inbound processing rules:

- Only text messages are processed in Part 1.
- External message IDs are deduplicated with the normalized `Provider + ExternalMessageId` uniqueness rule.
- Guest phone numbers are normalized to E.164-style values.
- StayFlow tries to resolve exactly one company-scoped guest.
- StayFlow then resolves a current reservation when possible.
- If reservation routing is ambiguous, the message is still persisted through the existing conversation model and human takeover is enabled before any autonomous reply can be sent.
- Unsupported message types are acknowledged safely and recorded as internal diagnostics without storing raw payloads.

Sanitized inbound payload example:

```json
{
  "object": "whatsapp_business_account",
  "entry": [
    {
      "changes": [
        {
          "field": "messages",
          "value": {
            "metadata": {
              "phone_number_id": "demo-phone-number-id"
            },
            "messages": [
              {
                "id": "wamid.demo-inbound-001",
                "from": "+1******1234",
                "timestamp": "1784975100",
                "type": "text",
                "text": {
                  "body": "Need check-in help"
                }
              }
            ]
          }
        }
      ]
    }
  ]
}
```

## Outbound Text Flow

Outbound visible host and AI replies continue to be persisted by `ConversationService` first.

For WhatsApp conversations:

1. `ConversationService` stores the message with `Provider=WhatsAppCloud` and `DeliveryStatus=Pending`.
2. `IConversationChannelDispatcher` selects `WhatsAppConversationChannelSender`.
3. `WhatsAppConversationChannelSender` resolves the company integration and sends through `IWhatsAppCloudClient`.
4. On success, the external provider message ID is stored and delivery moves to `Sent`.
5. On failure, the message remains in StayFlow and is marked `Failed` with sanitized failure details.
6. Host Inbox receives realtime message update events for delivery-state changes.

Sanitized outbound request example:

```json
{
  "messaging_product": "whatsapp",
  "recipient_type": "individual",
  "to": "+1******1234",
  "type": "text",
  "text": {
    "body": "Your check-in code is available in the guest guide."
  }
}
```

## Delivery Status Flow

Normalized outbound statuses:

- `Pending`
- `Sent`
- `Delivered`
- `Read`
- `Failed`

Rules:

- duplicate status events are harmless
- status progression does not move backward
- unknown statuses are ignored safely
- failure details are sanitized before storage
- inbound guest messages do not show outbound delivery labels

## Human Takeover

WhatsApp reuses the existing human takeover rules.

- If human takeover is active, AI does not auto-reply.
- Host Inbox replies on WhatsApp conversations are dispatched through the WhatsApp sender.
- Returning a conversation to AI mode restores the existing policy.
- Ambiguous reservation routing forces host attention before any autonomous reply.

## Development Mode

When `WhatsAppCloud:DevelopmentMode=true`, StayFlow uses `DevelopmentWhatsAppCloudClient`.

Development client behavior:

- does not call Meta
- generates deterministic fake external message IDs
- records sanitized outbound requests in memory for tests
- supports replaying delivery state changes through the simulator or tests

Development-only simulator endpoint:

- `POST /development/whatsapp/simulate`

This endpoint is available only in the Development environment. It does not bypass signature verification on the real webhook route.

## Signature Example For Local Simulation

Example shell flow for a signed local webhook request:

```bash
payload='{"object":"whatsapp_business_account","entry":[]}'
secret='dev-app-secret-change-me'
signature=$(printf '%s' "$payload" | openssl dgst -sha256 -hmac "$secret" -binary | xxd -p -c 256)
curl -i \
  -X POST http://localhost:5243/webhooks/whatsapp \
  -H "Content-Type: application/json" \
  -H "X-Hub-Signature-256: sha256=$signature" \
  --data "$payload"
```

## Codespaces And Meta Setup

Codespaces webhook URL pattern:

```text
https://<codespace-name>-5243.app.github.dev/webhooks/whatsapp
```

Meta setup checklist:

1. Create or open the Meta app that owns the WhatsApp Business integration.
2. Add the WhatsApp product.
3. Configure the callback URL and verify token.
4. Subscribe the app to the WhatsApp Business Account.
5. Confirm the app receives `messages` webhook changes.
6. Store secrets with user secrets or environment variables, not in source.

## Security And Logging Rules

- never log access tokens, app secrets, webhook verify tokens, or signatures
- never log guest message content at Information level
- mask phone numbers in logs, for example `+1******1234`
- do not persist raw webhook payloads by default
- do not persist secrets in the database
- request body size is capped on the webhook endpoint
- webhook traffic is rate limited with a named ASP.NET Core policy

## Database

Migration name:

- `AddWhatsAppMessagingFoundation`

Apply the database update:

```bash
cd /workspace
dotnet tool run dotnet-ef database update --project backend/backend.csproj --startup-project backend/backend.csproj
```

## Part 1 Limitations

- text messages only
- no media downloads
- no templates or campaigns
- no billing or onboarding UI
- no persistent unresolved-event inbox for ambiguous guest matches without a safe guest binding
- no durable outbox or retry worker beyond the current in-process channel dispatch
- secrets remain configuration based rather than per-company stored

## Planned Part 2 Extensions

- templates
- media and attachments
- interactive messages
- read receipt expansion if needed beyond current delivery state updates
- durable retry and outbox workflows
- per-company secret storage
- onboarding UI