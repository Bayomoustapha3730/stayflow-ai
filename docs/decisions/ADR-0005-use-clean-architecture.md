# ADR-0005: Use Clean Architecture Principles

## Status

Accepted

## Context

StayFlow AI will grow across companies, properties, guests, conversations, payments, AI workflows, and external integrations. The codebase needs clear boundaries so features can evolve without making controllers, database access, and business rules tightly coupled.

## Decision

Follow Clean Architecture principles using controllers, DTOs, services, repositories, models, middleware, and extension methods with clear responsibilities.

## Consequences

- Business behavior should live in services rather than controllers.
- Persistence concerns should live in repositories and EF Core configurations.
- DTOs should define API boundaries and protect internal entities.
- Tests can focus on service behavior with repository test doubles.
- The project may later split into separate assemblies if module boundaries require it.

## Alternatives Considered

- Transaction script controllers: faster initially, but risks large controllers and duplicated business rules.
- Full domain-driven design from the start: powerful, but heavier than needed for the current foundation.
- Minimal API-only structure: concise, but less aligned with the current layered backend style.
