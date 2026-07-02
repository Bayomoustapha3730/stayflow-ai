# 03 Multi-Tenancy

## Purpose

StayFlow AI is designed as a multi-tenant SaaS platform where each company can manage its own properties, guests, conversations, service requests, payments, and knowledge without accessing another company's data.

## Tenant Boundary

The initial tenant boundary is `CompanyId`. Company-scoped records should either include `CompanyId` directly or be reachable only through a company-scoped aggregate.

## Isolation Rules

- List, detail, update, and delete operations must filter by company scope.
- Services should validate company existence before creating company-scoped records.
- Repositories should include company filters in queries for sensitive data.
- Cross-company access should return not found or forbidden depending on the authenticated context.
- Tests should cover cross-company isolation for core workflows.

## Authentication Integration

Until full authenticated tenant context is available, APIs may require explicit `CompanyId`. Once authentication is fully enforced, company scope should come from the current user and authorization claims.

## Data Model Guidance

- Index `CompanyId` on frequently queried tenant-scoped tables.
- Avoid global queries that return tenant data without scope.
- Include audit logs for sensitive tenant operations.
- Use soft deletes where business history must be preserved.

## Risks

The main risk is accidental data leakage through missing company filters. Code review and tests should treat tenant isolation as a security requirement.
