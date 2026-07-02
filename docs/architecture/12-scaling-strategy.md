# 12 Scaling Strategy

## Purpose

The scaling strategy describes how StayFlow AI can grow as usage increases across companies, properties, guests, and WhatsApp conversations.

## Initial Scaling Approach

- Keep the API stateless where possible.
- Use PostgreSQL indexes for common company-scoped access patterns.
- Paginate list endpoints.
- Move slow work to background services.
- Add caching only when measured bottlenecks justify it.

## Application Scaling

The backend should support horizontal scaling by avoiding in-memory user state. Shared state should live in PostgreSQL, a distributed cache, or queue infrastructure when introduced.

## Database Scaling

- Index high-volume lookup columns such as `CompanyId`, `PropertyId`, `GuestId`, `PhoneNumber`, and `CreatedAt`.
- Monitor slow queries.
- Keep migrations reviewed and reversible where practical.
- Consider read replicas only after read load justifies them.

## Integration Scaling

WhatsApp and AI providers may enforce rate limits. The system should queue, throttle, and retry outbound work rather than overwhelming providers.

## Review Triggers

Revisit scaling architecture when message volume, tenant count, AI latency, or database load creates measurable operational pressure.
