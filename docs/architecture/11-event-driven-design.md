# 11 Event-Driven Design

## Purpose

Event-driven design helps StayFlow AI decouple workflows such as guest messaging, service requests, notifications, audits, and analytics.

## Event Candidates

- Company created.
- Property updated.
- Guest message received.
- Conversation escalated.
- Service request created.
- Payment received.
- AI response generated.
- Outbound message failed.

## Guidelines

- Use events for state changes that other workflows need to react to.
- Keep event payloads minimal and versionable.
- Include identifiers rather than full entity snapshots when possible.
- Make event handlers idempotent.
- Preserve tenant context with `CompanyId` where applicable.

## Current State

The current backend is request and service oriented. Event-driven infrastructure can be introduced incrementally when asynchronous workflows or cross-module coordination require it.

## Risks

Events can make flows harder to trace if observability is weak. Correlation IDs, structured logs, and clear event naming are required.
