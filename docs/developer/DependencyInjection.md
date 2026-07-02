# Dependency Injection

## Purpose

Dependency injection keeps StayFlow AI components testable, replaceable, and aligned with ASP.NET Core conventions.

## Guidelines

- Register services in extension methods under `Extensions` when possible.
- Use constructor injection for required dependencies.
- Avoid service locator patterns.
- Keep registrations explicit and easy to scan.
- Prefer interfaces for application services and repositories.

## Lifetimes

- Use `Scoped` for request-bound services and repositories.
- Use `Singleton` only for stateless services that do not depend on scoped services.
- Use `Transient` for lightweight stateless components that do not need shared request state.

## Configuration

- Bind options from configuration for grouped settings.
- Do not inject raw configuration widely when typed options would be clearer.
- Keep secrets in environment variables or secure secret stores, not source files.

## Testing

Dependency injection should make services easy to instantiate with fake repositories, test doubles, or in-memory dependencies.
