# Integration Testing

## Purpose

Integration tests verify that application components work together correctly across API endpoints, database persistence, Entity Framework configurations, middleware, and infrastructure adapters.

## Scope

- Web API request and response behavior.
- Entity Framework relationships, indexes, constraints, and migrations.
- Repository and service integration.
- Middleware behavior for errors, correlation IDs, and standardized responses.
- Authentication and authorization flows when implemented.
- Provider adapter contracts for WhatsApp, AI, email, and payments.

## Guidelines

- Use realistic test fixtures for company, property, guest, reservation, and billing data.
- Verify company isolation across reads and writes.
- Test soft-delete behavior and audit field updates.
- Prefer database-backed tests for persistence behavior.
- Keep external provider calls stubbed, mocked, or sandboxed.

## Database Checks

- Migrations apply cleanly.
- Required relationships enforce expected behavior.
- Index-backed queries support common filters such as company, property, guest, phone number, and created date.
- Seed data does not break application startup.

## API Checks

- Validate status codes, response shape, correlation IDs, error arrays, and data payloads.
- Confirm pagination metadata where applicable.
- Confirm validation errors are clear and deterministic.

## Acceptance Criteria

- Integration tests cover critical API and persistence workflows.
- Tests prove tenant isolation for shared tables.
- Provider integrations can be validated without hitting production systems.
