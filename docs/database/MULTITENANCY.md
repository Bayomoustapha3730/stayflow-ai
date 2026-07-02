# Multitenancy

## Purpose

This document defines database-level expectations for multi-company isolation in StayFlow AI.

## Tenant Boundary

The initial tenant boundary is `CompanyId`. Company-scoped tables should include `CompanyId` directly unless they are strictly owned through another company-scoped aggregate.

## Data Access Rules

- Queries must filter by company scope for tenant-owned data.
- Updates and soft deletes must validate company scope.
- Cross-company access should not reveal whether another company's record exists.
- Background jobs must preserve tenant context.
- Tests should cover tenant-isolation behavior for sensitive workflows.

## Schema Guidance

- Index `CompanyId` on frequently queried company-scoped tables.
- Include foreign keys that preserve relationship integrity.
- Avoid nullable `CompanyId` on records that must always belong to a tenant.
- Use audit logs for operations that affect tenant-owned data.

## Future Enhancements

Future database isolation options may include row-level security, tenant-specific schemas, or separate databases for enterprise customers. Those options should be evaluated through ADRs before implementation.
