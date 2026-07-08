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

Authenticated APIs must resolve company scope from verified user claims, currently the `company_id` JWT claim issued by StayFlow AI authentication. Client-supplied `CompanyId` values must not be accepted as tenant selectors for protected workflows such as Property management.

If an authenticated request does not contain a valid tenant claim, tenant-scoped services should reject the operation before executing repository queries.

## Property Tenant Enforcement

Property endpoints use authenticated tenant context for create, list, detail, update, and delete workflows. The Property repository also applies `CompanyId` filters so tenant isolation does not depend only on controller routing.

Soft-deleted properties are excluded by EF Core global query filters. Tenant-aware global query filters are not currently placed in `ApplicationDbContext` because request-scoped tenant state in the DbContext would require additional lifecycle design. Until an ADR changes this, tenant isolation is enforced by service and repository boundaries plus tests.

## Data Model Guidance

- Index `CompanyId` on frequently queried tenant-scoped tables.
- Avoid global queries that return tenant data without scope.
- Include audit logs for sensitive tenant operations.
- Use soft deletes where business history must be preserved.

## Risks

The main risk is accidental data leakage through missing company filters. Code review and tests should treat tenant isolation as a security requirement.
