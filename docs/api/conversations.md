# Conversations API

The Conversations API is an authenticated staff API for tenant-scoped conversation management.

## Create Or Reuse Conversation

`POST /conversations`

```json
{
  "guestId": "44444444-4444-4444-4444-444444444444",
  "reservationId": "55555555-5555-5555-5555-555555555555",
  "channel": "Web",
  "channelIdentity": "email:demo.guest@stayflow.local",
  "subject": "Check-in question"
}
```

```json
{
  "success": true,
  "message": "Conversation created successfully.",
  "data": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "guestId": "44444444-4444-4444-4444-444444444444",
    "reservationId": "55555555-5555-5555-5555-555555555555",
    "channel": "Web",
    "status": "Open",
    "humanTakeoverEnabled": false
  },
  "errors": [],
  "correlationId": "..."
}
```

## Get Conversation

`GET /conversations/{conversationId}`

Returns conversation identity, guest summary, optional reservation/property summaries, assignment state, status, and guest-visible messages.

## Get Messages

`GET /conversations/{conversationId}/messages?pageNumber=1&pageSize=20`

Internal notes are excluded unless `includeInternal=true` is supplied by an authorized staff client.

## Add Host Message

`POST /conversations/{conversationId}/messages/host`

```json
{
  "content": "Thanks for your message. I will confirm this for you shortly."
}
```

## Add Internal Note

`POST /conversations/{conversationId}/notes`

```json
{
  "content": "Host confirmed late checkout is available."
}
```

Internal notes are host-only and must not be shown in guest-facing history.

## Escalate Conversation

`POST /conversations/{conversationId}/escalate`

```json
{
  "reason": "Guest asked for approval that requires host decision."
}
```

## Human Takeover

`POST /conversations/{conversationId}/human-takeover`

Enables host-managed mode and blocks automatic AI replies.

## Return To AI

`POST /conversations/{conversationId}/return-to-ai`

Disables human takeover and returns the conversation to `Open`.

## Resolve

`POST /conversations/{conversationId}/resolve`

Marks the conversation as resolved.

## Close

`POST /conversations/{conversationId}/close`

Closes the conversation. Closed conversations reject ordinary new messages.

## Permissions

- `conversations.read`
- `conversations.create`
- `conversations.reply`
- `conversations.escalate`
- `conversations.manage`
- `conversations.notes`
