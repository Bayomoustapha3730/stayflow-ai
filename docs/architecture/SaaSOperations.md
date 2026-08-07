# SaaS Operations Architecture

## Context

Sprint 20D extends existing Sprint 17-20 foundations into a centralized SaaS operations layer.

Design objective: add an operator control plane without replacing core product services.

## Architectural Approach

- Reuse existing entities (`Company`, `TenantSubscription`, `UsageRecord`, `BillingWebhookEvent`, `AuditLog`).
- Extend existing `PlatformAdminController` instead of introducing parallel controllers.
- Maintain strict authorization (`platform.admin`) and explicit permission gates.
- Preserve tenant context boundaries in host-facing services and endpoints.

## Control Plane Domains

- Platform administration dashboard
- Tenant management and lifecycle control
- Usage analytics and organization health
- Feature flag administration (plan entitlement backed)
- Operational monitoring (jobs, webhooks, queues, email)
- Provider and billing health
- Support impersonation (audited)
- Incident and diagnostics views

## Observability Coverage

The operations APIs expose metrics for:

- API usage
- SignalR activity indicators
- AI usage/provider state
- Billing invoice/webhook health
- Email token issuance indicators
- WhatsApp usage/provider state
- Background job failure indicators
- Database record health indicators
- Queue depth estimate/unknown marker
- Health issue indicators

## Data and Audit Model

Mutating platform operations emit audit logs with:

- actor identity
- operation action
- target entity id
- safe metadata payload

No secrets or raw credentials are serialized to audit payloads.

## Tradeoffs

- Queue depth cannot be measured exactly with current in-memory channel implementation.
- Some provider signals are heuristic until dedicated persistent telemetry entities are introduced.
- Support impersonation session state is audit-backed and intentionally minimal for hardening-first rollout.
