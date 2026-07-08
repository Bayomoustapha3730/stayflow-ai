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
- Resolve company, guest, and reservation context before stay-specific AI processing, following [ADR-0007](../decisions/ADR-0007-reservation-context-resolution.md).
- If multiple reservation candidates remain, request clarification with non-sensitive labels or escalate rather than asking AI to guess.

## Response and Logging

Webhook endpoints should avoid returning sensitive details. Logs should include safe identifiers such as message ID, company ID, property ID, and correlation ID where available.

Reservation context resolution logs should include candidate counts, resolution outcome, selected reservation ID when resolved, clarification requested, escalation reason, and security validation result. Logs must not include sensitive access secrets.

## Future Documentation

Add WhatsApp Cloud API webhook payload examples, verification flow, retry behavior, and outbound message schemas.
