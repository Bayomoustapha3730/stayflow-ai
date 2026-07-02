# ADR-0001: Use ASP.NET Core for the Backend API

## Status

Accepted

## Context

StayFlow AI needs a reliable backend API for property managers, Airbnb hosts, guest concierge workflows, authentication, integrations, and operational tooling. The platform requires strong typing, mature web APIs, dependency injection, testability, and PostgreSQL support.

## Decision

Use ASP.NET Core Web API as the backend framework for StayFlow AI.

## Consequences

- The backend benefits from built-in dependency injection, middleware, configuration, health checks, and OpenAPI support.
- C# and .NET provide strong typing and mature tooling for maintainable business applications.
- The team should follow ASP.NET Core conventions for controllers, services, middleware, and configuration.
- Contributors need familiarity with .NET, Entity Framework Core, and ASP.NET Core testing patterns.

## Alternatives Considered

- Node.js with Express or NestJS: strong ecosystem, but less aligned with the current typed backend architecture.
- Python with FastAPI: productive and AI-friendly, but less suitable for the current service-layer and EF Core direction.
- Java with Spring Boot: mature enterprise option, but heavier for the initial team and project setup.
