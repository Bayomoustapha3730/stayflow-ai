# 05 WhatsApp Architecture

## Purpose

WhatsApp is the primary guest communication channel for StayFlow AI. The architecture must support inbound messages, outbound replies, delivery status updates, retries, and operational visibility.

## Core Flow

1. WhatsApp sends a webhook event to the backend.
2. The webhook endpoint validates the request.
3. The message is persisted with company, property, guest, and conversation context when available.
4. The AI orchestrator or rule-based workflow determines the response.
5. The response is sent through WhatsApp Cloud API.
6. Delivery and read-status callbacks update message state.

## Design Guidelines

- Keep webhook handlers fast and resilient.
- Use background processing for slow AI or external API work.
- Persist inbound events before processing when possible.
- Use idempotency keys or message IDs to avoid duplicate processing.
- Respect WhatsApp template, opt-in, and messaging window policies.

## Failure Handling

- Retry transient failures.
- Record failed outbound messages for operator review.
- Avoid sending duplicate replies.
- Log provider errors with correlation and message identifiers.

## Security

Webhook validation, signature verification, and secret management are required before production use.
