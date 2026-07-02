# ADR-0006: Use Multi-Tenant SaaS Design

## Status

Accepted

## Context

StayFlow AI serves multiple Airbnb hosts and property management companies. Each company must only access its own users, properties, guests, conversations, knowledge, service requests, and payments. The system must support growth without mixing tenant data.

## Decision

Design StayFlow AI as a multi-tenant SaaS platform using company-scoped data isolation as the initial tenancy boundary.

## Consequences

- Company-scoped entities must include `CompanyId` or be reachable only through a company-scoped aggregate.
- Queries must filter by company scope for reads, updates, and deletes.
- Future authentication and authorization should derive company scope from the authenticated user.
- Tests should cover cross-company isolation for sensitive workflows.
- Database indexes should support company-scoped access patterns.

## Alternatives Considered

- Single-tenant deployments per customer: stronger isolation, but operationally expensive for early growth.
- Separate database per company: strong isolation, but adds migration and operational complexity.
- Shared database without tenant scope: simpler initially, but unacceptable for security and customer trust.
