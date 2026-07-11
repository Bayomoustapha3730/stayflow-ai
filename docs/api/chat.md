# Chat API

Sprint 2 chat endpoints require authentication. Anonymous guest access is not enabled yet; guest access tokens will be added later.

## Send Message

`POST /chat/message`

```json
{
  "guestId": "44444444-4444-4444-4444-444444444444",
  "reservationId": "55555555-5555-5555-5555-555555555555",
  "propertyId": "22222222-2222-2222-2222-222222222222",
  "message": "What are my check-in and check-out dates?",
  "channel": "Web",
  "currentTimestamp": "2026-07-11T12:00:00Z"
}
```

```json
{
  "success": true,
  "message": "Chat message processed successfully.",
  "data": {
    "conversationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "conversationStatus": "Open",
    "guestMessage": {
      "senderType": "Guest",
      "content": "What are my check-in and check-out dates?",
      "messageType": "Text"
    },
    "assistantMessage": {
      "senderType": "AI",
      "content": "Your check-in date is ...",
      "messageType": "Text"
    },
    "humanTakeoverEnabled": false,
    "requiresHostAttention": false
  },
  "errors": [],
  "correlationId": "..."
}
```

## Get Conversation

`GET /chat/{conversationId}`

Returns a guest-safe summary with status, channel, subject, human takeover state, safe reservation context, and recent guest-visible messages.

## Get History

`GET /chat/{conversationId}/history?pageNumber=1&pageSize=20`

History is chronological and excludes internal notes, audit data, provider diagnostics, deleted messages, and host-only records.

## Escalate

`POST /chat/{conversationId}/escalate`

```json
{
  "guestId": "44444444-4444-4444-4444-444444444444",
  "reason": "I need help from the host."
}
```

Returns an updated guest-safe status and enables host attention.

## End

`POST /chat/{conversationId}/end`

```json
{
  "guestId": "44444444-4444-4444-4444-444444444444"
}
```

Closes the conversation. Sending a later message without `ConversationId` starts a new compatible conversation.

## Permissions

- `chat.send`
- `chat.read`
- `chat.escalate`
- `chat.end`
