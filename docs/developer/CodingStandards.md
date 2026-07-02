# Coding Standards

## Purpose

Coding standards keep StayFlow AI predictable, readable, and safe to extend. Contributors should follow the existing project style unless a documented decision changes the convention.

## General Guidelines

- Write code that is easy to read, test, and review.
- Keep changes scoped to the requested feature or fix.
- Prefer simple types and explicit control flow over unnecessary abstractions.
- Avoid duplicating business rules across controllers, services, and repositories.
- Use nullable reference type annotations intentionally.
- Avoid hard-coded secrets, credentials, environment-specific URLs, or API keys.

## C# and ASP.NET Core

- Use `async` and `await` for database, network, and file I/O.
- Accept `CancellationToken` in controller, service, and repository methods where work may be cancelled.
- Keep controllers responsible for HTTP concerns only.
- Return standardized `ApiResponse<T>` objects from API workflows.
- Validate incoming DTOs before applying business logic.
- Prefer constructor injection for dependencies.
- Keep DTOs separate from EF Core entities.

## Tests

- Add or update tests when behavior changes.
- Use focused tests that describe observable behavior.
- Include negative-path tests for validation, authorization, tenant isolation, and not-found cases.

## Documentation

Update documentation when a change affects setup, public API behavior, architecture, security, deployment, or operating procedures.
