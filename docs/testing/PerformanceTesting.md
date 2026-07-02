# Performance Testing

## Purpose

Performance testing verifies that StayFlow AI remains responsive under expected operational load, especially for WhatsApp concierge replies, dashboard queries, search, and background processing.

## Key Workflows

- Guest message intake and AI response preparation.
- Property, guest, reservation, and company search.
- Marketplace request creation and status updates.
- Billing invoice and payment lookup.
- Authentication and current-user endpoint calls.
- Dashboard summaries and analytics queries.

## Metrics

- API latency by endpoint.
- Database query duration.
- AI orchestration latency.
- Queue or background job processing time.
- Error rate under load.
- Resource usage for CPU, memory, and database connections.

## Guidelines

- Establish baseline measurements before optimization.
- Test realistic data volumes for companies, properties, guests, conversations, and reservations.
- Include multi-tenant query patterns.
- Identify N+1 queries and missing indexes.
- Separate provider latency from application latency when possible.

## Test Types

- Smoke performance tests for pull requests touching hot paths.
- Load tests for release readiness.
- Soak tests for background jobs and long-running workloads.
- Spike tests for sudden WhatsApp traffic bursts.

## Acceptance Criteria

- Critical guest-facing endpoints meet agreed latency targets.
- Search and pagination remain stable at realistic data volumes.
- Performance regressions are detected before release.
