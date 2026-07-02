# 02 Clean Architecture

## Purpose

StayFlow AI follows Clean Architecture principles to keep business behavior independent from HTTP transport, database access, and external service integrations.

## Layer Responsibilities

- **Controllers** handle HTTP routing, status codes, request binding, and response shaping.
- **DTOs** define API request and response contracts.
- **Services** contain business rules, validation orchestration, and workflow coordination.
- **Repositories** encapsulate persistence queries and database writes.
- **Models** represent domain and persistence entities.
- **Data Configurations** define EF Core relationships, indexes, constraints, and table mappings.
- **Middleware** handles cross-cutting HTTP concerns such as error handling, authorization, and correlation IDs.
- **Extensions** organize dependency registration and application setup.

## Dependency Direction

Application flow should move inward from transport to business logic to persistence abstractions. Controllers should depend on services, services should depend on repositories, and repositories should depend on the EF Core `DbContext`.

## Guidelines

- Do not place business logic in controllers.
- Do not expose EF Core entities directly as API contracts.
- Keep repository methods focused and query-specific.
- Keep service methods asynchronous when they call I/O.
- Add tests at the service layer for business workflows.

## Evolution

The current project uses folders within one ASP.NET Core project. If the codebase grows, the same boundaries can be split into separate assemblies without changing the conceptual architecture.
