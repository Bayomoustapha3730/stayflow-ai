# Unit Testing

## Purpose

Unit tests verify individual components in isolation. They should be fast, deterministic, and focused on business rules, validation, mapping, and service behavior.

## Scope

- Domain validation rules.
- Service layer decisions.
- DTO mapping.
- Pagination and filtering logic.
- Standardized API response creation.
- AI context filtering and guard rail rules.

## Guidelines

- Test behavior, not implementation details.
- Use clear arrange, act, assert structure.
- Prefer small fixtures over large shared test objects.
- Mock external dependencies such as email, WhatsApp, AI providers, payment providers, and repositories.
- Cover success, validation failure, authorization boundary, and edge cases.

## Naming

Use descriptive test names that explain the behavior:

```text
MethodName_WhenCondition_ShouldExpectedResult
```

## Example Scenarios

- Company creation rejects empty names.
- Property search returns only active records for the current company.
- Guest phone normalization handles Kenyan numbers consistently.
- Reservation checkout date cannot be before check-in date.
- AI context excludes sensitive guest data.

## Quality Expectations

- Unit tests should run quickly in local development and CI.
- Tests should not require a real database, network, clock, or file system unless explicitly isolated.
- Time-dependent logic should use an injectable clock abstraction when implemented.

## Acceptance Criteria

- New business logic includes focused unit tests.
- Tests fail for meaningful behavior regressions.
- Unit tests remain readable enough to serve as executable documentation.
