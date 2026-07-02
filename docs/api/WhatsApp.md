# WhatsApp API

## Purpose

WhatsApp APIs and webhooks support guest-facing concierge conversations through WhatsApp Cloud API.

## Planned Endpoint Areas

- Webhook verification.
- Inbound message callbacks.
- Message status callbacks.
- Outbound message dispatch.
- Conversation lookup and escalation workflows.

## Webhook Considerations

- Validate webhook signatures or provider verification tokens.
- Persist inbound events before long-running processing.
- Use idempotency to avoid duplicate message handling.
- Return quickly and move slow work to background services.

## Response and Logging

Webhook endpoints should avoid returning sensitive details. Logs should include safe identifiers such as message ID, company ID, property ID, and correlation ID where available.

## Future Documentation

Add WhatsApp Cloud API webhook payload examples, verification flow, retry behavior, and outbound message schemas.
