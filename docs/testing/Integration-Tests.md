# Integration Tests

## Purpose

Integration tests verify that multiple StayFlow AI components work together correctly, especially API endpoints, database behavior, middleware, authentication, and external integration boundaries.

## Scope

Use integration tests for:

- Controller and middleware behavior.
- Entity Framework Core mappings and migrations.
- Authentication and authorization flows.
- Repository queries against a real or test database.
- API response format and status code behavior.
- Webhook handling and provider callback contracts.

## Guidelines

- Use isolated test data.
- Reset database state between tests or test runs.
- Avoid depending on production services.
- Replace external providers with controlled test doubles.
- Validate tenant and company isolation at API and persistence boundaries.

## Database Considerations

When integration tests require PostgreSQL, prefer a repeatable local or containerized setup. Migrations should be applied consistently before tests run.

## Future Work

Define the official integration test infrastructure once external provider modules and webhook workflows are implemented.
