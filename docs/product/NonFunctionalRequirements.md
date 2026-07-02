# Non-Functional Requirements

## Purpose

This document defines quality attributes and operating expectations for StayFlow AI.

## Security

- Enforce authentication and authorization for protected workflows.
- Preserve company and tenant isolation.
- Protect secrets, tokens, payment metadata, and guest data.
- Use audit logging for sensitive operations.

## Reliability

- Keep API workflows predictable and resilient.
- Handle external provider failures gracefully.
- Use retries and background processing where appropriate.
- Preserve idempotency for webhooks and payment callbacks.

## Performance

- Paginate list endpoints.
- Index common tenant and property access patterns.
- Avoid unbounded database queries.
- Monitor slow API, database, AI, and WhatsApp workflows.

## Scalability

- Keep API services stateless where practical.
- Support horizontal scaling as traffic grows.
- Move slow messaging and AI work to background services.
- Design analytics and reporting for growth.

## Maintainability

- Follow Clean Architecture principles.
- Keep DTOs separate from persistence models.
- Use repository and service layers consistently.
- Keep documentation and ADRs current.

## Observability

- Use structured logs.
- Preserve correlation IDs.
- Expose health checks.
- Track operational metrics and failure rates.
