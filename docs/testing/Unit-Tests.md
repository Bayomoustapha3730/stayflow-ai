# Unit Tests

## Purpose

Unit tests verify small pieces of StayFlow AI behavior in isolation. They should be fast, deterministic, and focused on business rules or component behavior.

## Scope

Use unit tests for:

- Service-layer business workflows.
- Validation logic.
- Mapping behavior.
- Authorization and permission helpers.
- Error-handling branches.
- Utility and domain logic.

## Guidelines

- Keep tests independent and repeatable.
- Use clear test names that describe behavior.
- Prefer fake repositories or test doubles over real databases.
- Cover success, validation failure, not-found, and tenant-isolation paths.
- Avoid testing framework internals or trivial property getters.

## Running Tests

Run the backend unit test project with:

```bash
dotnet test tests/StayFlow.Api.Tests/StayFlow.Api.Tests.csproj
```

## Quality Expectations

New business behavior should generally include unit tests unless the change is documentation-only, configuration-only, or covered more appropriately by integration tests.
