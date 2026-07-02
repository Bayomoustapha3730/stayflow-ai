# Performance Tests

## Purpose

Performance tests measure how StayFlow AI behaves under expected operating conditions and help identify slow endpoints, expensive queries, and integration bottlenecks.

## Scope

Use performance tests for:

- High-traffic API endpoints.
- Property, guest, conversation, and analytics queries.
- AI orchestration latency.
- WhatsApp webhook processing.
- Background job throughput.
- Database migration duration where relevant.

## Metrics

Track:

- Response time percentiles.
- Error rates.
- Database query duration.
- External provider latency.
- CPU, memory, and connection pool usage.
- Background queue processing time.

## Guidelines

- Define realistic data volumes.
- Measure before optimizing.
- Compare results against a baseline.
- Avoid mixing performance tests with regular unit test suites.
- Document environment, dataset, and test parameters.

## Future Work

Create repeatable performance scenarios when guest messaging, AI orchestration, and analytics modules mature.
